using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace StajEvrak.Core;

/*
 * Belge okuma katmanı.
 * DocumentParser soyut sınıfı her dosya türü için ortak arayüzü tanımlar.
 * DocxParser, PdfParser, ImageParser, LegacyDocParser bu arayüzü kendi yöntemleriyle
 * uygular; ParserFactory uzantıya göre doğru nesneyi döndürür (çok biçimlilik).
 * Okunan ham içerik Ek16FieldExtractor ile Ek-16 alanlarına eşlenir.
 *
 * Not: Görüntü dosyalarında el yazısı tanıma (OCR/HTR) final aşamasında entegre
 * edilecektir. Vize aşamasında görüntüler "manuel inceleme" olarak işaretlenir.
 */

/// <summary>Belgede bulunan 'etiket → değer' çifti.</summary>
public sealed class Segment
{
    public Segment(string label, string value, string section)
    {
        Label = label;
        Value = value;
        Section = section;
    }

    public string Label { get; }
    public string Value { get; set; }
    public string Section { get; }
}

public sealed class ParsedDocument
{
    public string Source { get; set; }
    public List<Segment> Segments { get; } = new();
    public List<string> Lines { get; } = new();
    public bool Readable { get; set; } = true;
    public string Note { get; set; } = "";
    public int ImageCount { get; set; }
}

public abstract class DocumentParser
{
    public const string SectionStudent = "student";
    public const string SectionCompany = "company";

    private static readonly Regex Separator = new(@"\s*[:：]\s*");
    private static readonly Regex Placeholder = new(@"^[\s._\-…/]*$");
    private static readonly Regex Dots = new(@"[.…_]{3,}");

    public abstract string SourceName { get; }
    public abstract ParsedDocument Parse(byte[] content);

    // --- Ortak yardımcılar (alt sınıflar tarafından kalıtımla kullanılır)
    protected static string CleanValue(string value)
    {
        value = (value ?? "").Replace(' ', ' ').Trim();
        value = Dots.Replace(value, " ").Trim(' ', ':');
        return Placeholder.IsMatch(value) ? "" : TextUtils.CollapseSpaces(value);
    }

    protected static string DetectSection(string text, string current)
    {
        var t = TextUtils.Fold(text);
        if (t.Length > 80) return current;
        if (new[] { "isyeri", "isveren", "firma bilgi", "kurum bilgi", "isletme" }.Any(k => t.Contains(k)))
            return SectionCompany;
        if (t.Contains("ogrenci") && (t.Contains("bilgi") || t.Length < 30))
            return SectionStudent;
        return current;
    }

    protected static List<Segment> SegmentsFromLine(string line, string section)
    {
        line = (line ?? "").Trim();
        var result = new List<Segment>();
        if (line.Length == 0) return result;
        var parts = Separator.Split(line, 2);
        if (parts.Length == 2 && parts[0].Length > 1 && parts[0].Length <= 60)
            result.Add(new Segment(parts[0].Trim(), CleanValue(parts[1]), section));
        return result;
    }

    /// <summary>Tablo satırı: [etiket, değer, etiket, değer, ...] veya 'etiket: değer' hücreleri.</summary>
    protected static (List<Segment> Segments, string Section) SegmentsFromRow(IList<string> cells, string section)
    {
        // birleştirilmiş hücre tekrarlarını kaldır
        var uniq = new List<string>();
        foreach (var raw in cells)
        {
            var c = (raw ?? "").Trim();
            if (uniq.Count == 0 || uniq[^1] != c) uniq.Add(c);
        }
        var segs = new List<Segment>();
        var nonEmpty = uniq.Where(c => c.Length > 0).ToList();
        if (nonEmpty.Count == 1 && uniq.Count <= 2)
        {
            var newSection = DetectSection(nonEmpty[0], section);
            if (newSection != section) return (segs, newSection);
            return (SegmentsFromLine(nonEmpty[0], section), section);
        }
        var i = 0;
        while (i < uniq.Count)
        {
            var cell = uniq[i];
            if (cell.Length == 0) { i++; continue; }
            var inline = SegmentsFromLine(cell, section);
            if (inline.Count > 0 && inline[0].Value.Length > 0)
            {
                segs.AddRange(inline);
                i++;
                continue;
            }
            var label = cell.TrimEnd(' ', ':');
            var value = i + 1 < uniq.Count ? uniq[i + 1] : "";
            segs.Add(new Segment(label, CleanValue(value), section));
            i += 2;
        }
        return (segs, section);
    }

    /// <summary>Tek satırlık metni işler: bölüm başlığı mı, 'etiket: değer' mi?</summary>
    protected static string HandleTextLine(ParsedDocument result, string line, string section)
    {
        result.Lines.Add(line);
        if (!line.Contains(':')) section = DetectSection(line, section);
        result.Segments.AddRange(SegmentsFromLine(line, section));
        return section;
    }
}

// ---------------------------------------------------------------------------
public sealed class DocxParser : DocumentParser
{
    public override string SourceName => "Word (.docx)";

    public override ParsedDocument Parse(byte[] content)
    {
        var result = new ParsedDocument { Source = SourceName };
        using var stream = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        string section = null;

        if (body != null)
        {
            foreach (var block in body.Elements())
            {
                if (block is W.Paragraph p)
                {
                    section = HandleTextLine(result, p.InnerText, section);
                }
                else if (block is W.Table table)
                {
                    foreach (var row in table.Elements<W.TableRow>())
                    {
                        var cells = row.Elements<W.TableCell>()
                            .Select(c => string.Join(" ", c.Elements<W.Paragraph>().Select(x => x.InnerText)))
                            .ToList();
                        result.Lines.Add(string.Join(" | ", cells));
                        var (segs, newSection) = SegmentsFromRow(cells, section);
                        section = newSection;
                        result.Segments.AddRange(segs);
                    }
                }
            }
        }

        result.ImageCount = doc.MainDocumentPart?.ImageParts.Count() ?? 0;
        if (result.Segments.Count == 0)
        {
            result.Readable = false;
            result.Note = "Word belgesinde okunabilir alan bulunamadı.";
        }
        return result;
    }
}

/// <summary>
/// PDF metin katmanını okur. Kelimeler konumlarına göre satırlara gruplanır; aynı satırdaki
/// büyük boşluklar tablo hücresi sınırı kabul edilir. Böylece 'etiket | değer' tabloları,
/// bölüm başlıkları ve alt satıra taşan değerler doğru sırayla işlenir.
/// </summary>
public sealed class PdfParser : DocumentParser
{
    private const double LineTolerance = 3.0;   // aynı satır sayılacak dikey fark (pt)
    private const double CellGap = 10.0;        // hücre sınırı sayılacak yatay boşluk (pt)

    public override string SourceName => "PDF";

    private sealed class Cell
    {
        public double X;
        public string Text;
    }

    public override ParsedDocument Parse(byte[] content)
    {
        var result = new ParsedDocument { Source = SourceName };
        string section = null;

        using (var pdf = PdfDocument.Open(content))
        {
            foreach (var page in pdf.GetPages())
            {
                double? lastValueX = null;
                Segment lastSegment = null;

                foreach (var cells in BuildLines(page))
                {
                    if (cells.Count >= 2)
                    {
                        var texts = cells.Select(c => c.Text).ToList();
                        result.Lines.Add(string.Join(" | ", texts));
                        var (segs, newSection) = SegmentsFromRow(texts, section);
                        section = newSection;
                        result.Segments.AddRange(segs);
                        lastValueX = cells[1].X;
                        lastSegment = segs.LastOrDefault();
                        continue;
                    }

                    var single = cells[0];
                    // Değer sütununda başlayan tek hücre → önceki değerin devamı (alt satıra taşmış adres vb.)
                    if (lastValueX.HasValue && lastSegment != null && single.X >= lastValueX.Value - 3)
                    {
                        lastSegment.Value = TextUtils.CollapseSpaces(lastSegment.Value + " " + CleanValue(single.Text));
                        result.Lines.Add(single.Text);
                        continue;
                    }
                    lastValueX = null;
                    lastSegment = null;
                    section = HandleTextLine(result, single.Text, section);
                }
                result.ImageCount += page.GetImages().Count();
            }
        }

        if (result.Lines.All(string.IsNullOrWhiteSpace))
        {
            result.Readable = false;
            result.Note = "PDF içinde metin katmanı yok (taranmış belge). El yazısı/taranmış belge okuma (OCR) " +
                          "final aşamasında entegre edilecektir; belge manuel incelemeye alındı.";
        }
        return result;
    }

    private static List<List<Cell>> BuildLines(Page page)
    {
        var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();

        var lines = new List<List<Word>>();
        foreach (var w in words)
        {
            var current = lines.Count > 0 ? lines[^1] : null;
            if (current != null && Math.Abs(current[0].BoundingBox.Bottom - w.BoundingBox.Bottom) <= LineTolerance)
                current.Add(w);
            else
                lines.Add(new List<Word> { w });
        }

        var result = new List<List<Cell>>();
        foreach (var line in lines)
        {
            var sorted = line.OrderBy(w => w.BoundingBox.Left).ToList();
            var cells = new List<Cell>();
            Cell cell = null;
            double prevRight = 0;
            foreach (var w in sorted)
            {
                if (cell == null || w.BoundingBox.Left - prevRight > CellGap)
                {
                    cell = new Cell { X = w.BoundingBox.Left, Text = w.Text };
                    cells.Add(cell);
                }
                else
                {
                    cell.Text += " " + w.Text;
                }
                prevRight = w.BoundingBox.Right;
            }
            result.Add(cells);
        }
        return result;
    }
}

/// <summary>JPEG / PNG: el yazısı tanıma (OCR/HTR) final aşamasında entegre edilecek.</summary>
public sealed class ImageParser : DocumentParser
{
    public override string SourceName => "Görüntü (JPEG/PNG)";

    public override ParsedDocument Parse(byte[] content) => new()
    {
        Source = SourceName,
        ImageCount = 1,
        Readable = false,
        Note = "Görüntüdeki el yazısı alanların otomatik okunması (OCR/HTR) final aşamasında entegre edilecektir. " +
               "Belge manuel incelemeye alındı."
    };
}

/// <summary>Eski Word 97-2003 (.doc) ikili formatı: kabul edilir ancak otomatik okunmaz.</summary>
public sealed class LegacyDocParser : DocumentParser
{
    public override string SourceName => "Word 97-2003 (.doc)";

    public override ParsedDocument Parse(byte[] content) => new()
    {
        Source = SourceName,
        Readable = false,
        Note = ".doc formatındaki belge otomatik okunamadı. Daha doğru sonuç için belgeyi .docx veya PDF olarak " +
               "yükleyebilirsiniz; belge manuel incelemeye alındı."
    };
}

public static class ParserFactory
{
    private static readonly Dictionary<string, Func<DocumentParser>> Parsers = new()
    {
        [".docx"] = () => new DocxParser(),
        [".pdf"] = () => new PdfParser(),
        [".jpg"] = () => new ImageParser(),
        [".jpeg"] = () => new ImageParser(),
        [".png"] = () => new ImageParser(),
        [".doc"] = () => new LegacyDocParser(),
    };

    public static DocumentParser ForExtension(string ext) =>
        Parsers.TryGetValue(ext.ToLowerInvariant(), out var create)
            ? create()
            : throw new ArgumentException($"{ext} için okuyucu yok");
}

// ---------------------------------------------------------------------------
/// <summary>Ham etiket/değer çiftlerini Ek-16 alanlarına eşler.</summary>
public sealed class Ek16FieldExtractor
{
    private static string Compact(string text) => TextUtils.Fold(text).Replace(" ", "");

    private static int Score(Ek16Field field, Segment seg)
    {
        var label = Compact(seg.Label);
        if (label.Length == 0) return 0;
        var aliases = field.Aliases.Select(Compact).ToList();

        var sectionOk = field.Group == Ek16Schema.GroupCompany || seg.Section != DocumentParser.SectionCompany;
        if (field.Group == Ek16Schema.GroupStudent && seg.Section == DocumentParser.SectionCompany) sectionOk = false;

        if (aliases.Contains(label)) return sectionOk ? 4 : 1;
        if (field.Group == Ek16Schema.GroupCompany && seg.Section == DocumentParser.SectionCompany &&
            field.SectionAliases.Select(Compact).Contains(label))
            return 3;
        if (sectionOk && aliases.Any(a => a.Length >= 5 && label.StartsWith(a))) return 2;
        return 0;
    }

    public Dictionary<string, string> Extract(ParsedDocument parsed)
    {
        var used = new HashSet<int>();
        var found = new Dictionary<string, string>();
        foreach (var field in Ek16Schema.Fields)
        {
            int best = -1, bestScore = 0;
            for (var i = 0; i < parsed.Segments.Count; i++)
            {
                if (used.Contains(i)) continue;
                var s = Score(field, parsed.Segments[i]);
                var better = s > bestScore ||
                             (s == bestScore && s > 0 && best >= 0 && parsed.Segments[best].Value.Length == 0 &&
                              parsed.Segments[i].Value.Length > 0);
                if (better)
                {
                    best = i;
                    bestScore = s;
                }
            }
            if (best >= 0 && bestScore >= 2)
            {
                used.Add(best);
                found[field.Key] = parsed.Segments[best].Value;
            }
        }
        return found;
    }
}
