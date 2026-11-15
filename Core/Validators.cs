using System.IO.Compression;
using System.Text;

namespace StajEvrak.Core;

/*
 * Evrak yükleme kontrolleri (Bölüm 4.4 — vize kapsamı: dosya formatı ve uzantı).
 * Her kontrol FileValidator soyut sınıfından türetilir ve Validate() metodunu kendine
 * göre uygular (çok biçimlilik). UploadValidationPipeline bu kontrolleri sırayla çalıştırır.
 */

/// <summary>Yüklenen dosyanın bellekteki temsili.</summary>
public sealed class UploadedFile
{
    public UploadedFile(string fileName, byte[] content)
    {
        FileName = Path.GetFileName(fileName ?? "");
        Content = content ?? Array.Empty<byte>();
    }

    public string FileName { get; }
    public byte[] Content { get; }
    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();
    public long Size => Content.LongLength;

    public static async Task<UploadedFile> FromFormFileAsync(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null) return new UploadedFile("", Array.Empty<byte>());
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return new UploadedFile(file.FileName, ms.ToArray());
    }
}

public sealed class ValidationResult
{
    public bool Ok { get; private set; } = true;
    public List<string> Errors { get; } = new();

    public void Add(string message)
    {
        Ok = false;
        Errors.Add(message);
    }
}

public abstract class FileValidator
{
    public abstract string Name { get; }

    /// <summary>Hata varsa açıklama, yoksa null döner.</summary>
    public abstract string Validate(UploadedFile file);
}

public sealed class EmptyFileValidator : FileValidator
{
    public override string Name => "Dosya seçimi";

    public override string Validate(UploadedFile file)
    {
        if (string.IsNullOrEmpty(file.FileName)) return "Lütfen yüklenecek bir dosya seçin.";
        if (file.Size == 0) return "Seçilen dosya boş.";
        return null;
    }
}

public sealed class ExtensionValidator : FileValidator
{
    private static readonly HashSet<string> SuspiciousInner = new()
    {
        ".exe", ".bat", ".cmd", ".js", ".vbs", ".sh", ".php", ".html"
    };

    private readonly HashSet<string> _allowed;
    private readonly string _description;

    public ExtensionValidator(IEnumerable<string> allowed, string description)
    {
        _allowed = new HashSet<string>(allowed.Select(e => e.ToLowerInvariant()));
        _description = description;
    }

    public override string Name => "Uzantı";

    public override string Validate(UploadedFile file)
    {
        var ext = file.Extension;
        if (string.IsNullOrEmpty(ext))
            return $"Dosyanın uzantısı yok. Kabul edilen formatlar: {_description}.";
        if (!_allowed.Contains(ext))
            return $"'{ext}' formatı kabul edilmiyor. Yalnızca {_description} dosyaları yüklenebilir.";
        // belge.exe.pdf gibi çift uzantılı şüpheli dosyalar reddedilir
        var inner = Path.GetExtension(Path.GetFileNameWithoutExtension(file.FileName)).ToLowerInvariant();
        if (SuspiciousInner.Contains(inner))
            return "Dosya adında şüpheli çift uzantı tespit edildi.";
        return null;
    }
}

public sealed class SizeValidator : FileValidator
{
    private readonly long _maxBytes;

    public SizeValidator(long maxBytes) => _maxBytes = maxBytes;

    public override string Name => "Boyut";

    public override string Validate(UploadedFile file) =>
        file.Size > _maxBytes
            ? $"Dosya boyutu {_maxBytes / (1024 * 1024)} MB sınırını aşıyor ({file.Size / 1048576.0:0.0} MB)."
            : null;
}

/// <summary>
/// Dosya içeriğinin (magic bytes) uzantıyla uyumlu olduğunu doğrular.
/// Örn. adı 'belge.pdf' olan ama aslında başka türde olan yüklemeler reddedilir.
/// </summary>
public sealed class SignatureValidator : FileValidator
{
    private static readonly Dictionary<string, byte[]> Signatures = new()
    {
        [".pdf"] = Encoding.ASCII.GetBytes("%PDF-"),
        [".png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
        [".jpg"] = new byte[] { 0xFF, 0xD8, 0xFF },
        [".jpeg"] = new byte[] { 0xFF, 0xD8, 0xFF },
        [".doc"] = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 },
        [".docx"] = new byte[] { 0x50, 0x4B, 0x03, 0x04 },
    };

    public override string Name => "Dosya içeriği";

    public override string Validate(UploadedFile file)
    {
        if (!Signatures.TryGetValue(file.Extension, out var sig)) return null;
        if (file.Content.Length < sig.Length || !new ReadOnlySpan<byte>(file.Content, 0, sig.Length).SequenceEqual(new ReadOnlySpan<byte>(sig)))
            return $"Dosya içeriği '{file.Extension}' formatıyla uyuşmuyor. Dosya bozuk olabilir veya uzantısı değiştirilmiş olabilir.";

        if (file.Extension == ".docx")
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
                if (zip.GetEntry("word/document.xml") == null)
                    return "Dosya geçerli bir Word (.docx) belgesi değil.";
            }
            catch (InvalidDataException)
            {
                return "Word (.docx) dosyası bozuk veya okunamıyor.";
            }
        }

        if (file.Extension == ".pdf")
        {
            var tailLength = Math.Min(2048, file.Content.Length);
            var tail = new ReadOnlySpan<byte>(file.Content, file.Content.Length - tailLength, tailLength);
            if (tail.IndexOf(new ReadOnlySpan<byte>(Encoding.ASCII.GetBytes("%%EOF"))) < 0)
                return "PDF dosyası eksik veya bozuk görünüyor.";
        }
        return null;
    }
}

public sealed class UploadValidationPipeline
{
    private readonly List<FileValidator> _validators;

    public UploadValidationPipeline(IEnumerable<FileValidator> validators) => _validators = validators.ToList();

    public IReadOnlyList<string> Checks => _validators.Select(v => v.Name).ToList();

    public ValidationResult Run(UploadedFile file, bool stopOnFirst = true)
    {
        var result = new ValidationResult();
        foreach (var validator in _validators)
        {
            var error = validator.Validate(file); // çok biçimli çağrı
            if (error == null) continue;
            result.Add(error);
            if (stopOnFirst) break;
        }
        return result;
    }

    public static UploadValidationPipeline ForDocuments(IEnumerable<string> allowed, long maxBytes) => new(new FileValidator[]
    {
        new EmptyFileValidator(),
        new ExtensionValidator(allowed, "Word (.doc/.docx), PDF, JPEG ve PNG"),
        new SizeValidator(maxBytes),
        new SignatureValidator(),
    });

    public static UploadValidationPipeline ForDatasets(IEnumerable<string> allowed, long maxBytes) => new(new FileValidator[]
    {
        new EmptyFileValidator(),
        new ExtensionValidator(allowed, "CSV (.csv) ve Excel (.xlsx)"),
        new SizeValidator(maxBytes),
    });
}
