using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using StajEvrak.Data;

namespace StajEvrak.Core;

/*
 * domain nesneleri — NYP prensipleri:
 *  - Soyutlama      : User soyut sınıftır; her rol kendi davranışını tanımlar.
 *  - Kalıtım        : Student ve Admin, User sınıfından türetilir.
 *  - Çok biçimlilik : Home, CanModify(), CanView(), MenuItems her rolde farklı çalışır.
 *  - Kapsülleme     : Şifre özeti, hatalı deneme sayısı, kilit süresi ve başvuru statüsü
 *                     private alanlardır; yalnızca kontrollü metotlarla değiştirilir.
 */

public record MenuItem(string Controller, string Action, string Label, string Icon);

public abstract class User
{
    private string _passwordHash;
    private int _failedAttempts;
    private DateTime? _lockedUntil;

    protected User(int id, string username, string email, string fullName, string passwordHash,
                   int failedAttempts, DateTime? lockedUntil, string createdAt)
    {
        Id = id;
        Username = username;
        Email = email;
        FullName = fullName;
        _passwordHash = passwordHash;
        _failedAttempts = failedAttempts;
        _lockedUntil = lockedUntil;
        CreatedAt = createdAt;
    }

    public int Id { get; }
    public string Username { get; }
    public string Email { get; }
    public string FullName { get; }
    public string CreatedAt { get; }

    public string PasswordHash => _passwordHash;
    public int FailedAttempts => _failedAttempts;
    public DateTime? LockedUntil => _lockedUntil;

    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0]);
            var s = new string(parts.ToArray()).ToUpper(new CultureInfo("tr-TR"));
            return s.Length > 0 ? s : "?";
        }
    }

    public bool IsLocked(DateTime? now = null) => _lockedUntil.HasValue && _lockedUntil.Value > (now ?? DateTime.Now);

    /// <summary>Hatalı denemeyi kaydeder; limit aşılırsa hesabı kilitler. Kilitlendiyse true döner.</summary>
    public bool RegisterFailedAttempt(int maxAttempts, DateTime lockUntil)
    {
        _failedAttempts++;
        if (_failedAttempts >= maxAttempts)
        {
            _lockedUntil = lockUntil;
            return true;
        }
        return false;
    }

    public void ResetFailedAttempts()
    {
        _failedAttempts = 0;
        _lockedUntil = null;
    }

    public void ChangePasswordHash(string newHash)
    {
        if (string.IsNullOrEmpty(newHash)) throw new ArgumentException("Şifre özeti boş olamaz");
        _passwordHash = newHash;
    }

    // --- Rol bazlı (soyut) davranışlar
    public abstract Role Role { get; }
    public abstract string RoleLabel { get; }
    public abstract MenuItem Home { get; }
    public abstract bool CanView(Application application);
    /// <summary>Bu kullanıcı başvuruyu / belgeyi güncelleyebilir veya silebilir mi?</summary>
    public abstract bool CanModify(Application application);
    public abstract IReadOnlyList<MenuItem> MenuItems { get; }

    public override string ToString() => $"<{GetType().Name} {Username}>";
}

public sealed class Student : User
{
    public Student(int id, string username, string email, string fullName, string passwordHash,
                   int failedAttempts, DateTime? lockedUntil, string createdAt, string studentNo, string tcKimlikNo)
        : base(id, username, email, fullName, passwordHash, failedAttempts, lockedUntil, createdAt)
    {
        StudentNo = studentNo;
        TcKimlikNo = tcKimlikNo;
    }

    public string StudentNo { get; }
    public string TcKimlikNo { get; }

    public override Role Role => Role.Student;
    public override string RoleLabel => "Öğrenci";
    public override MenuItem Home => new("Student", "Dashboard", "Panelim", "grid");

    public override bool CanView(Application application) => application.StudentId == Id;

    // Öğrenci yalnızca kendi başvurusunu, Tamamlandı değilse değiştirebilir.
    public override bool CanModify(Application application) =>
        CanView(application) && !application.Status.IsLockedForStudent();

    public override IReadOnlyList<MenuItem> MenuItems => new[]
    {
        new MenuItem("Student", "Dashboard", "Panelim", "grid"),
        new MenuItem("Student", "NewApplication", "Yeni Başvuru", "plus"),
        new MenuItem("Student", "Profile", "Profilim", "user"),
    };
}

public sealed class Admin : User
{
    public Admin(int id, string username, string email, string fullName, string passwordHash,
                 int failedAttempts, DateTime? lockedUntil, string createdAt)
        : base(id, username, email, fullName, passwordHash, failedAttempts, lockedUntil, createdAt) { }

    public override Role Role => Role.Admin;
    public override string RoleLabel => "Yönetici";
    public override MenuItem Home => new("Admin", "Dashboard", "Genel Bakış", "grid");

    public override bool CanView(Application application) => true;

    // Admin, Tamamlandı statüsündeki belgeleri de güncelleyebilir / silebilir.
    public override bool CanModify(Application application) => true;

    public override IReadOnlyList<MenuItem> MenuItems => new[]
    {
        new MenuItem("Admin", "Dashboard", "Genel Bakış", "grid"),
        new MenuItem("Admin", "Applications", "Başvurular", "file"),
        new MenuItem("Admin", "Completed", "Tamamlandı", "check"),
        new MenuItem("Admin", "Dataset", "Veri Seti", "database"),
        new MenuItem("Admin", "Users", "Kullanıcılar", "users"),
        new MenuItem("Admin", "LoginLogs", "Giriş Kayıtları", "shield"),
    };
}

/// <summary>Veritabanı satırından doğru alt sınıfı üretir (çok biçimli oluşturma).</summary>
public static class UserFactory
{
    public static User FromEntity(UserEntity e)
    {
        var role = RoleExtensions.FromCode(e.Role);
        var created = TextUtils.Format(e.CreatedAt);
        return role switch
        {
            Role.Student => new Student(e.Id, e.Username, e.Email, e.FullName, e.PasswordHash, e.FailedAttempts,
                e.LockedUntil, created, e.StudentNo, e.TcKimlikNo),
            Role.Admin => new Admin(e.Id, e.Username, e.Email, e.FullName, e.PasswordHash, e.FailedAttempts,
                e.LockedUntil, created),
            _ => throw new InvalidOperationException()
        };
    }
}

// ---------------------------------------------------------------------------
// Başvuru / Belge
// ---------------------------------------------------------------------------
public class Document
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string OriginalName { get; set; }
    public string StoredName { get; set; }
    public string Extension { get; set; }
    public long SizeBytes { get; set; }
    public Dictionary<string, string> ExtractedData { get; set; } = new();
    public ComparisonReport Report { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string UploadedAt { get; set; }

    public string SizeLabel
    {
        get
        {
            var kb = SizeBytes / 1024.0;
            return kb > 1024 ? $"{kb / 1024:0.00} MB" : $"{kb:0} KB";
        }
    }

    public string KindLabel => Extension switch
    {
        ".pdf" => "PDF",
        ".docx" or ".doc" => "Word",
        ".jpg" or ".jpeg" => "JPEG",
        ".png" => "PNG",
        _ => Extension.ToUpperInvariant()
    };

    public static Document FromEntity(DocumentEntity e) => new()
    {
        Id = e.Id,
        ApplicationId = e.ApplicationId,
        OriginalName = e.OriginalName,
        StoredName = e.StoredName,
        Extension = e.Extension,
        SizeBytes = e.SizeBytes,
        ExtractedData = Json.Read<Dictionary<string, string>>(e.ExtractedData) ?? new(),
        Report = Json.Read<ComparisonReport>(e.Report),
        IsCurrent = e.IsCurrent,
        UploadedAt = TextUtils.Format(e.UploadedAt),
    };
}

/// <summary>Bir öğrencinin Ek-16 başvurusu. Statü yalnızca ChangeStatus ile değiştirilebilir.</summary>
public class Application
{
    private DocumentStatus _status;

    public Application(int id, int studentId, DocumentStatus status, Dictionary<string, string> formData,
                       List<string> stajGunleri, string googleFormUrl, string adminNote,
                       string createdAt, string updatedAt, string completedAt)
    {
        Id = id;
        StudentId = studentId;
        _status = status;
        FormData = formData ?? new();
        StajGunleri = stajGunleri ?? new();
        GoogleFormUrl = googleFormUrl;
        AdminNote = adminNote;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CompletedAt = completedAt;
    }

    public int Id { get; }
    public int StudentId { get; }
    public Dictionary<string, string> FormData { get; set; }
    public List<string> StajGunleri { get; set; }
    public string GoogleFormUrl { get; set; }
    public string AdminNote { get; set; }
    public string CreatedAt { get; }
    public string UpdatedAt { get; set; }
    public string CompletedAt { get; private set; }

    // İlişkili nesneler (repository tarafından doldurulur)
    public List<Document> Documents { get; set; } = new();
    public string StudentName { get; set; }
    public string StudentNo { get; set; }

    public DocumentStatus Status => _status;

    public string Field(string key) => FormData.TryGetValue(key, out var v) ? v ?? "" : "";

    /// <summary>
    /// Statü değiştirir. Öğrenci/sistem yalnızca izinli geçişleri yapabilir; admin her geçişi yapabilir.
    /// Eski statüyü döndürür.
    /// </summary>
    public DocumentStatus ChangeStatus(DocumentStatus target, User actor)
    {
        var old = _status;
        if (old == target) return old;
        if (actor is not Admin)
        {
            if (old.IsLockedForStudent())
                throw new UnauthorizedAccessException("Tamamlandı statüsündeki belge değiştirilemez.");
            if (!old.CanTransitionTo(target))
                throw new InvalidOperationException($"{old.Label()} → {target.Label()} geçişine izin verilmiyor.");
        }
        _status = target;
        if (target == DocumentStatus.TAMAMLANDI)
            CompletedAt = TextUtils.NowString();
        return old;
    }

    public Document CurrentDocument => Documents.FirstOrDefault(d => d.IsCurrent);

    public static Application FromEntity(ApplicationEntity e)
    {
        var app = new Application(
            e.Id, e.StudentId, Enum.Parse<DocumentStatus>(e.Status),
            Json.Read<Dictionary<string, string>>(e.FormData),
            Json.Read<List<string>>(e.StajGunleri),
            e.GoogleFormUrl, e.AdminNote, TextUtils.Format(e.CreatedAt), TextUtils.Format(e.UpdatedAt),
            e.CompletedAt.HasValue ? TextUtils.Format(e.CompletedAt.Value) : null);
        if (e.Student != null)
        {
            app.StudentName = e.Student.FullName;
            app.StudentNo = e.Student.StudentNo;
        }
        if (e.Documents != null)
            app.Documents = e.Documents.OrderByDescending(d => d.Id).Select(Document.FromEntity).ToList();
        return app;
    }
}

public class DatasetRecord
{
    public int Id { get; set; }
    public string AdSoyad { get; set; }
    public string TcKimlikNo { get; set; }
    public string Uyruk { get; set; }
    public string OgrenciNo { get; set; }
    public string DogumTarihi { get; set; }
    public string IkametgahAdresi { get; set; }
    public string Bolum { get; set; }
    public string TeslimYili { get; set; }
    public int? OncekiStajGun { get; set; }
    public string Eposta { get; set; }
    public string Telefon { get; set; }
    public string SaglikYardimi { get; set; }
    public string ImportedAt { get; set; }
    public string SourceFile { get; set; }

    /// <summary>Sütun anahtarına göre değer (veri seti karşılaştırması ve tablolar için).</summary>
    public string Get(string key) => key switch
    {
        "ad_soyad" => AdSoyad,
        "tc_kimlik_no" => TcKimlikNo,
        "uyruk" => Uyruk,
        "ogrenci_no" => OgrenciNo,
        "dogum_tarihi" => DogumTarihi,
        "ikametgah_adresi" => IkametgahAdresi,
        "bolum" => Bolum,
        "teslim_yili" => TeslimYili,
        "onceki_staj_gun" => OncekiStajGun?.ToString(),
        "eposta" => Eposta,
        "telefon" => Telefon,
        "saglik_yardimi" => SaglikYardimi,
        _ => null
    };
}

public static class Json
{
    // Türkçe karakterler \uXXXX yerine olduğu gibi yazılır (veritabanında arama yapılabilmesi için)
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch (JsonException) { return null; }
    }
}
