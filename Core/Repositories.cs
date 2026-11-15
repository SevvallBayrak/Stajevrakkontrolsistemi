using Microsoft.EntityFrameworkCore;
using StajEvrak.Data;

namespace StajEvrak.Core;

/*
 * Veri erişim katmanı (Repository deseni) — Entity Framework Core + LINQ.
 * BaseRepository<TEntity, TModel> soyut sınıfı ortak işlemleri (Get, Count, Delete) tanımlar;
 * alt sınıflar yalnızca entity → alan nesnesi dönüşümünü (ToModel) belirler (soyutlama + kalıtım).
 * Her metot kendi DbContext'ini açar ve kapatır (using).
 */
public abstract class BaseRepository<TEntity, TModel> where TEntity : class where TModel : class
{
    protected readonly AppDbContextFactory Factory;

    protected BaseRepository(AppDbContextFactory factory) => Factory = factory;

    protected abstract TModel ToModel(TEntity entity);

    public virtual TModel Get(int id)
    {
        using var db = Factory.Create();
        var entity = db.Set<TEntity>().Find(id);
        return entity == null ? null : ToModel(entity);
    }

    public int Count()
    {
        using var db = Factory.Create();
        return db.Set<TEntity>().Count();
    }

    public virtual void Delete(int id)
    {
        using var db = Factory.Create();
        var entity = db.Set<TEntity>().Find(id);
        if (entity == null) return;
        db.Remove(entity);
        db.SaveChanges();
    }
}

// okuma modelleri (ekranlar için)
public sealed class UserListItem
{
    public int Id { get; init; }
    public string Username { get; init; }
    public string Email { get; init; }
    public string Role { get; init; }
    public string FullName { get; init; }
    public string StudentNo { get; init; }
    public int FailedAttempts { get; init; }
    public DateTime? LockedUntil { get; init; }
    public DateTime CreatedAt { get; init; }
    public int ApplicationCount { get; init; }

    public bool IsLocked(DateTime now) => LockedUntil.HasValue && LockedUntil.Value > now;
}

public sealed class StatusHistoryItem
{
    public string OldStatus { get; init; }
    public string NewStatus { get; init; }
    public string Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public string ActorName { get; init; }
}

// ---------------------------------------------------------------------------
public sealed class UserRepository : BaseRepository<UserEntity, User>
{
    public UserRepository(AppDbContextFactory factory) : base(factory) { }

    protected override User ToModel(UserEntity entity) => UserFactory.FromEntity(entity);

    public User FindByLogin(string identifier)
    {
        var key = (identifier ?? "").Trim().ToLowerInvariant();
        using var db = Factory.Create();
        var entity = db.Users.AsNoTracking()
            .FirstOrDefault(u => u.Username.ToLower() == key || u.Email.ToLower() == key);
        return entity == null ? null : ToModel(entity);
    }

    public (bool Username, bool Email) Exists(string username, string email)
    {
        var u = username.ToLowerInvariant();
        var e = email.ToLowerInvariant();
        using var db = Factory.Create();
        return (db.Users.Any(x => x.Username.ToLower() == u), db.Users.Any(x => x.Email.ToLower() == e));
    }

    public bool StudentNoTaken(string studentNo)
    {
        using var db = Factory.Create();
        return db.Users.Any(x => x.StudentNo == studentNo);
    }

    public int Create(string username, string email, string passwordHash, Role role, string fullName,
                      string studentNo = null, string tcKimlikNo = null)
    {
        using var db = Factory.Create();
        var entity = new UserEntity
        {
            Username = username, Email = email, PasswordHash = passwordHash, Role = role.Code(),
            FullName = fullName, StudentNo = studentNo, TcKimlikNo = tcKimlikNo, CreatedAt = DateTime.Now,
        };
        db.Users.Add(entity);
        db.SaveChanges();
        return entity.Id;
    }

    /// <summary>Hatalı deneme sayısı, kilit süresi ve şifre özetini kaydeder.</summary>
    public void SaveSecurityState(User user)
    {
        using var db = Factory.Create();
        var entity = db.Users.Find(user.Id);
        if (entity == null) return;
        entity.FailedAttempts = user.FailedAttempts;
        entity.LockedUntil = user.LockedUntil;
        entity.PasswordHash = user.PasswordHash;
        db.SaveChanges();
    }

    public int CountByRole(Role role)
    {
        var code = role.Code();
        using var db = Factory.Create();
        return db.Users.Count(u => u.Role == code);
    }

    public int CountLocked(DateTime now)
    {
        using var db = Factory.Create();
        return db.Users.Count(u => u.LockedUntil != null && u.LockedUntil > now);
    }

    public List<UserListItem> ListWithStats()
    {
        using var db = Factory.Create();
        return db.Users.AsNoTracking()
            .OrderByDescending(u => u.Role).ThenByDescending(u => u.CreatedAt)
            .Select(u => new UserListItem
            {
                Id = u.Id, Username = u.Username, Email = u.Email, Role = u.Role, FullName = u.FullName,
                StudentNo = u.StudentNo, FailedAttempts = u.FailedAttempts, LockedUntil = u.LockedUntil,
                CreatedAt = u.CreatedAt, ApplicationCount = u.Applications.Count,
            })
            .ToList();
    }
}

// ---------------------------------------------------------------------------
/// <summary>Sunucu tarafı oturum token'ları. Çıkışta token iptal edilir (Bölüm 4.1).</summary>
public sealed class SessionRepository
{
    private readonly AppDbContextFactory _factory;

    public SessionRepository(AppDbContextFactory factory) => _factory = factory;

    public void Create(string token, int userId, Role role, DateTime expiresAt)
    {
        using var db = _factory.Create();
        db.Sessions.Add(new UserSessionEntity
        {
            Token = token, UserId = userId, Role = role.Code(), CreatedAt = DateTime.Now, ExpiresAt = expiresAt,
        });
        db.SaveChanges();
    }

    public UserSessionEntity GetValid(string token)
    {
        var now = DateTime.Now;
        using var db = _factory.Create();
        return db.Sessions.AsNoTracking().FirstOrDefault(s => s.Token == token && !s.Revoked && s.ExpiresAt > now);
    }

    public void Revoke(string token)
    {
        using var db = _factory.Create();
        var session = db.Sessions.Find(token);
        if (session == null) return;
        session.Revoked = true;
        db.SaveChanges();
    }
}

// ---------------------------------------------------------------------------
public sealed class LoginAttemptRepository
{
    private readonly AppDbContextFactory _factory;

    public LoginAttemptRepository(AppDbContextFactory factory) => _factory = factory;

    public void Log(string identifier, int? userId, string ip, bool success, string reason)
    {
        using var db = _factory.Create();
        db.LoginAttempts.Add(new LoginAttemptEntity
        {
            Identifier = identifier, UserId = userId, IpAddress = ip, Success = success, Reason = reason,
            CreatedAt = DateTime.Now,
        });
        db.SaveChanges();
    }

    public List<LoginAttemptEntity> Recent(int limit = 300, bool onlyFailed = false)
    {
        using var db = _factory.Create();
        var query = db.LoginAttempts.AsNoTracking();
        if (onlyFailed) query = query.Where(a => !a.Success);
        return query.OrderByDescending(a => a.Id).Take(limit).ToList();
    }

    public int FailedCountToday()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        using var db = _factory.Create();
        return db.LoginAttempts.Count(a => !a.Success && a.CreatedAt >= today && a.CreatedAt < tomorrow);
    }
}

// ---------------------------------------------------------------------------
public sealed class DatasetRepository : BaseRepository<DatasetRecord, DatasetRecord>
{
    public DatasetRepository(AppDbContextFactory factory) : base(factory) { }

    protected override DatasetRecord ToModel(DatasetRecord entity) => entity;

    public (int Inserted, int Updated) UpsertMany(IEnumerable<DatasetRecord> records, string sourceFile)
    {
        int inserted = 0, updated = 0;
        var now = TextUtils.NowString();
        using var db = Factory.Create();
        foreach (var r in records)
        {
            var existing = db.DatasetRecords.FirstOrDefault(x => x.TcKimlikNo == r.TcKimlikNo || x.OgrenciNo == r.OgrenciNo);
            if (existing != null)
            {
                existing.AdSoyad = r.AdSoyad;
                existing.TcKimlikNo = r.TcKimlikNo;
                existing.Uyruk = r.Uyruk;
                existing.OgrenciNo = r.OgrenciNo;
                existing.DogumTarihi = r.DogumTarihi;
                existing.IkametgahAdresi = r.IkametgahAdresi;
                existing.Bolum = r.Bolum;
                existing.TeslimYili = r.TeslimYili;
                existing.OncekiStajGun = r.OncekiStajGun;
                existing.Eposta = r.Eposta;
                existing.Telefon = r.Telefon;
                existing.SaglikYardimi = r.SaglikYardimi;
                existing.ImportedAt = now;
                existing.SourceFile = sourceFile;
                updated++;
            }
            else
            {
                r.Id = 0;
                r.ImportedAt = now;
                r.SourceFile = sourceFile;
                db.DatasetRecords.Add(r);
                inserted++;
            }
        }
        db.SaveChanges();
        return (inserted, updated);
    }

    public void Clear()
    {
        using var db = Factory.Create();
        db.DatasetRecords.RemoveRange(db.DatasetRecords);
        db.SaveChanges();
    }

    public List<DatasetRecord> AllOrdered()
    {
        using var db = Factory.Create();
        return db.DatasetRecords.AsNoTracking().OrderBy(x => x.AdSoyad).ToList();
    }

    public DatasetRecord FindForStudent(string tc, string studentNo)
    {
        tc ??= "";
        studentNo ??= "";
        if (tc == "" && studentNo == "") return null;
        using var db = Factory.Create();
        return db.DatasetRecords.AsNoTracking()
            .FirstOrDefault(x => (tc != "" && x.TcKimlikNo == tc) || (studentNo != "" && x.OgrenciNo == studentNo));
    }

    public List<DatasetRecord> Search(string q)
    {
        using var db = Factory.Create();
        return db.DatasetRecords.AsNoTracking()
            .Where(x => x.AdSoyad.Contains(q) || x.TcKimlikNo.Contains(q) || x.OgrenciNo.Contains(q) ||
                        (x.Bolum != null && x.Bolum.Contains(q)))
            .OrderBy(x => x.AdSoyad)
            .ToList();
    }
}

// ---------------------------------------------------------------------------
public sealed class ApplicationRepository : BaseRepository<ApplicationEntity, Application>
{
    public ApplicationRepository(AppDbContextFactory factory) : base(factory) { }

    protected override Application ToModel(ApplicationEntity entity) => Application.FromEntity(entity);

    /// <summary>Öğrenci adı ve yalnızca güncel belge ile birlikte liste sorgusu.</summary>
    private static IQueryable<ApplicationEntity> ListQuery(AppDbContext db) =>
        db.Applications.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.Documents.Where(d => d.IsCurrent));

    public override Application Get(int id)
    {
        using var db = Factory.Create();
        var entity = db.Applications.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.Documents)
            .FirstOrDefault(a => a.Id == id);
        return entity == null ? null : ToModel(entity);
    }

    public List<Application> ForStudent(int studentId)
    {
        using var db = Factory.Create();
        return ListQuery(db).Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.UpdatedAt)
            .AsEnumerable().Select(ToModel).ToList();
    }

    public List<Application> Search(string status = null, string q = null, bool excludeCompleted = false)
    {
        using var db = Factory.Create();
        var query = ListQuery(db);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);
        if (excludeCompleted)
        {
            var done = DocumentStatus.TAMAMLANDI.ToString();
            query = query.Where(a => a.Status != done);
        }
        if (!string.IsNullOrEmpty(q))
            query = query.Where(a => a.Student.FullName.Contains(q) ||
                                     (a.Student.StudentNo != null && a.Student.StudentNo.Contains(q)) ||
                                     a.FormData.Contains(q));
        return query.OrderByDescending(a => a.UpdatedAt).AsEnumerable().Select(ToModel).ToList();
    }

    public int Create(int studentId, Dictionary<string, string> formData, List<string> stajGunleri, string googleFormUrl)
    {
        using var db = Factory.Create();
        var now = DateTime.Now;
        var entity = new ApplicationEntity
        {
            StudentId = studentId, Status = DocumentStatus.TASLAK.ToString(), FormData = Json.Write(formData),
            StajGunleri = Json.Write(stajGunleri), GoogleFormUrl = googleFormUrl, CreatedAt = now, UpdatedAt = now,
        };
        entity.History.Add(new StatusHistoryEntity
        {
            NewStatus = DocumentStatus.TASLAK.ToString(), ChangedBy = studentId, Note = "Başvuru oluşturuldu", CreatedAt = now,
        });
        db.Applications.Add(entity);
        db.SaveChanges();
        return entity.Id;
    }

    /// <summary>Alan nesnesindeki değişiklikleri (statü, form bilgileri, not) veritabanına yazar.</summary>
    public void Save(Application app)
    {
        using var db = Factory.Create();
        var entity = db.Applications.Find(app.Id);
        if (entity == null) return;
        entity.Status = app.Status.ToString();
        entity.FormData = Json.Write(app.FormData);
        entity.StajGunleri = Json.Write(app.StajGunleri);
        entity.GoogleFormUrl = app.GoogleFormUrl;
        entity.AdminNote = app.AdminNote;
        entity.CompletedAt = TextUtils.ParseDateTime(app.CompletedAt);
        entity.UpdatedAt = DateTime.Now;
        db.SaveChanges();
    }

    public override void Delete(int id)
    {
        using var db = Factory.Create();
        var entity = db.Applications
            .Include(a => a.Documents)
            .Include(a => a.History)
            .FirstOrDefault(a => a.Id == id);
        if (entity == null) return;
        db.Applications.Remove(entity);   // belgeler ve geçmiş de silinir (cascade)
        db.SaveChanges();
    }

    public void LogStatus(int appId, DocumentStatus? oldStatus, DocumentStatus newStatus, int? actorId, string note = "")
    {
        using var db = Factory.Create();
        db.StatusHistory.Add(new StatusHistoryEntity
        {
            ApplicationId = appId, OldStatus = oldStatus?.ToString(), NewStatus = newStatus.ToString(),
            ChangedBy = actorId, Note = note, CreatedAt = DateTime.Now,
        });
        db.SaveChanges();
    }

    public List<StatusHistoryItem> History(int appId)
    {
        using var db = Factory.Create();
        return db.StatusHistory.AsNoTracking()
            .Where(h => h.ApplicationId == appId)
            .OrderByDescending(h => h.Id)
            .Select(h => new StatusHistoryItem
            {
                OldStatus = h.OldStatus, NewStatus = h.NewStatus, Note = h.Note, CreatedAt = h.CreatedAt,
                ActorName = h.ChangedByUser != null ? h.ChangedByUser.FullName : null,
            })
            .ToList();
    }

    public Dictionary<DocumentStatus, int> StatusCounts()
    {
        var counts = Enum.GetValues<DocumentStatus>().ToDictionary(s => s, _ => 0);
        using var db = Factory.Create();
        var rows = db.Applications.GroupBy(a => a.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToList();
        foreach (var r in rows)
            if (DocumentStatusExtensions.TryParseStatus(r.Status, out var s))
                counts[s] = r.Count;
        return counts;
    }
}

// ---------------------------------------------------------------------------
public sealed class DocumentRepository : BaseRepository<DocumentEntity, Document>
{
    public DocumentRepository(AppDbContextFactory factory) : base(factory) { }

    protected override Document ToModel(DocumentEntity entity) => Document.FromEntity(entity);

    /// <summary>Yeni belgeyi ekler; başvurunun önceki belgeleri "güncel değil" olarak işaretlenir.</summary>
    public int Add(Document doc)
    {
        using var db = Factory.Create();
        foreach (var old in db.Documents.Where(d => d.ApplicationId == doc.ApplicationId && d.IsCurrent).ToList())
            old.IsCurrent = false;
        var entity = new DocumentEntity
        {
            ApplicationId = doc.ApplicationId, OriginalName = doc.OriginalName, StoredName = doc.StoredName,
            Extension = doc.Extension, SizeBytes = doc.SizeBytes, ExtractedData = Json.Write(doc.ExtractedData),
            Report = doc.Report == null ? null : Json.Write(doc.Report), IsCurrent = true, UploadedAt = DateTime.Now,
        };
        db.Documents.Add(entity);
        db.SaveChanges();
        return entity.Id;
    }

    public void UpdateAnalysis(int docId, Dictionary<string, string> extracted, ComparisonReport report)
    {
        using var db = Factory.Create();
        var entity = db.Documents.Find(docId);
        if (entity == null) return;
        entity.ExtractedData = Json.Write(extracted);
        entity.Report = Json.Write(report);
        db.SaveChanges();
    }
}
