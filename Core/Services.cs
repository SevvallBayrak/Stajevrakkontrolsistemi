using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StajEvrak.Data;

namespace StajEvrak.Core;

/// <summary>İş mantığı katmanı: başvuru oluşturma, evrak yükleme, analiz ve statü yönetimi.</summary>
public sealed class OperationResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
    public List<string> Errors { get; init; } = new();
    public object Payload { get; init; }

    public static OperationResult Success(string message, object payload = null) => new() { Ok = true, Message = message, Payload = payload };
    public static OperationResult Fail(string message, List<string> errors = null, object payload = null) =>
        new() { Ok = false, Message = message, Errors = errors ?? new List<string> { message }, Payload = payload };
}

/// <summary>Formdan okunan Ek-16 verisi.</summary>
public sealed class ApplicationFormData
{
    public Dictionary<string, string> Data { get; init; } = new();
    public List<string> Days { get; init; } = new();
    public string Url { get; init; }
    public List<string> Errors { get; init; } = new();
    public string UrlError { get; init; }

    public bool Ok => Errors.Count == 0 && UrlError == null;
}

/// <summary>Arayüzden gelen Ek-16 form verisini okur ve temel doğrulamaları yapar.</summary>
public sealed class ApplicationFormParser
{
    public static readonly Regex GoogleFormRe = new(
        @"^https://(docs\.google\.com/forms/(u/\d+/)?d/(e/)?[A-Za-z0-9_-]{10,}(/(viewform|edit|formResponse))?([?#].*)?|forms\.gle/[A-Za-z0-9_-]{6,})$");

    public ApplicationFormData Parse(IFormCollection form)
    {
        var data = new Dictionary<string, string>();
        var errors = new List<string>();

        foreach (var f in Ek16Schema.Fields)
        {
            var value = form[f.Key].ToString().Trim();
            if (f.InputType != "textarea") value = TextUtils.CollapseSpaces(value);
            data[f.Key] = value;
            if (f.Required && value.Length == 0) errors.Add($"'{f.Label}' alanı boş bırakılamaz.");
        }

        var tc = data["tc_kimlik_no"];
        if (tc.Length > 0 && !(tc.Length == 11 && tc.All(char.IsAsciiDigit) && tc[0] != '0'))
            errors.Add("T.C. / Yabancı kimlik numarası 11 haneli olmalı ve 0 ile başlamamalıdır.");
        foreach (var key in new[] { "eposta", "firma_eposta" })
            if (data[key].Length > 0 && !RegistrationValidator.EmailRe.IsMatch(data[key]))
                errors.Add($"'{Ek16Schema.FieldsByKey[key].Label}' geçerli bir e-posta değil.");

        var start = TextUtils.ParseDate(data["staj_baslangic"]);
        var end = TextUtils.ParseDate(data["staj_bitis"]);
        if (start.HasValue && end.HasValue && end < start)
            errors.Add("Staj bitiş tarihi başlangıç tarihinden önce olamaz.");
        foreach (var key in new[] { "staj_gun_sayisi", "muhendis_sayisi" })
            if (data[key].Length > 0 && !data[key].All(char.IsAsciiDigit))
                errors.Add($"'{Ek16Schema.FieldsByKey[key].Label}' sayı olmalıdır.");

        var valid = Ek16Schema.Weekdays.Select(w => w.Value).ToHashSet();
        var days = form["staj_gunleri"].Where(d => d != null && valid.Contains(d)).Distinct().ToList();
        if (days.Count == 0) errors.Add("Staj yapılacak haftalık günlerden en az birini seçiniz.");

        var url = form["google_form_url"].ToString().Trim();
        string urlError = null;
        if (url.Length > 0 && !GoogleFormRe.IsMatch(url))
            urlError = "Girilen Google Form URL'si geçerli değil. Örnek: https://docs.google.com/forms/d/e/.../viewform veya https://forms.gle/...";

        return new ApplicationFormData
        {
            Data = data, Days = days, Url = url.Length > 0 ? url : null, Errors = errors, UrlError = urlError
        };
    }
}

public sealed class ApplicationService
{
    private readonly ApplicationRepository _apps;
    private readonly DocumentRepository _docs;
    private readonly DatasetRepository _dataset;
    private readonly string _uploadFolder;
    private readonly UploadValidationPipeline _pipeline;
    private readonly ApplicationFormParser _formParser = new();
    private readonly Ek16FieldExtractor _extractor = new();
    private readonly DocumentComparator _comparator = new();

    public ApplicationService(ApplicationRepository apps, DocumentRepository docs, DatasetRepository dataset,
                              string uploadFolder, UploadValidationPipeline pipeline)
    {
        _apps = apps;
        _docs = docs;
        _dataset = dataset;
        _uploadFolder = uploadFolder;
        _pipeline = pipeline;
    }

    public IReadOnlyList<string> ValidationChecks => _pipeline.Checks;

    public ApplicationFormData ParseForm(IFormCollection form) => _formParser.Parse(form);

    // ------------------------------------------------------------------ form
    public OperationResult Create(Student student, ApplicationFormData form)
    {
        if (!form.Ok) return OperationResult.Fail("Form hatalı", form.Errors);
        var id = _apps.Create(student.Id, form.Data, form.Days, form.Url);
        return OperationResult.Success("Başvuru bilgileri kaydedildi. Şimdi Ek-16 belgenizi yükleyebilirsiniz.", id);
    }

    public OperationResult Update(User actor, Application app, ApplicationFormData form)
    {
        if (!actor.CanModify(app)) return OperationResult.Fail("Tamamlandı statüsündeki belgeler güncellenemez.");
        if (!form.Ok) return OperationResult.Fail("Form hatalı", form.Errors);
        app.FormData = form.Data;
        app.StajGunleri = form.Days;
        app.GoogleFormUrl = form.Url;
        _apps.Save(app);
        if (app.CurrentDocument != null && app.Status != DocumentStatus.TAMAMLANDI)
            Analyze(app, actor, "Form bilgileri güncellendi, yeniden karşılaştırıldı");
        return OperationResult.Success("Başvuru bilgileri güncellendi.");
    }

    // ------------------------------------------------------------------ yükleme
    public OperationResult Upload(User actor, Application app, UploadedFile file)
    {
        if (!actor.CanModify(app)) return OperationResult.Fail("Tamamlandı statüsündeki belgeye yeni evrak yüklenemez.");
        var check = _pipeline.Run(file);
        if (!check.Ok) return OperationResult.Fail(check.Errors[0], check.Errors);

        var folder = Path.Combine(_uploadFolder, app.Id.ToString());
        Directory.CreateDirectory(folder);
        var stored = Guid.NewGuid().ToString("N") + file.Extension;
        File.WriteAllBytes(Path.Combine(folder, stored), file.Content);

        var name = file.FileName.Length > 200 ? file.FileName[..200] : file.FileName;
        var doc = new Document
        {
            ApplicationId = app.Id, OriginalName = name, StoredName = stored,
            Extension = file.Extension, SizeBytes = file.Size,
        };
        doc.Id = _docs.Add(doc);
        foreach (var d in app.Documents) d.IsCurrent = false;
        app.Documents.Insert(0, doc);

        Transition(app, DocumentStatus.YUKLENDI, actor, $"Evrak yüklendi: {doc.OriginalName}");
        Analyze(app, actor);
        return OperationResult.Success("Evrak başarıyla yüklendi ve analiz edildi.", doc);
    }

    // analiz
    public ComparisonReport Analyze(Application app, User actor, string note = "")
    {
        var doc = app.CurrentDocument;
        if (doc == null) return null;
        Transition(app, DocumentStatus.INCELENIYOR, actor, string.IsNullOrEmpty(note) ? "Belge analiz ediliyor" : note);

        var parser = ParserFactory.ForExtension(doc.Extension); // çok biçimli okuyucu seçimi
        ParsedDocument parsed;
        try
        {
            parsed = parser.Parse(File.ReadAllBytes(FilePath(app, doc)));
        }
        catch (Exception ex)
        {
            parsed = new ParsedDocument { Source = parser.SourceName, Readable = false, Note = $"Belge okunurken hata oluştu: {ex.Message}" };
        }

        var extracted = parsed.Readable ? _extractor.Extract(parsed) : new Dictionary<string, string>();
        var dataset = _dataset.FindForStudent(app.Field("tc_kimlik_no"), app.Field("ogrenci_no"));
        var report = _comparator.Compare(app.FormData, parsed, extracted, dataset);
        _docs.UpdateAnalysis(doc.Id, extracted, report);
        doc.ExtractedData = extracted;
        doc.Report = report;

        if (report.ManualReview)
            _apps.Save(app); // İnceleniyor statüsünde kalır (manuel inceleme)
        else if (report.Ready)
            Transition(app, DocumentStatus.ONAYLANDI, actor, "Otomatik kontrol başarılı — teslime hazır");
        else
            Transition(app, DocumentStatus.HATALI_EKSIK, actor, string.Join("; ", report.Messages.Take(2)));
        return report;
    }

    // statü
    private void Transition(Application app, DocumentStatus target, User actor, string note)
    {
        var old = app.ChangeStatus(target, actor);
        _apps.Save(app);
        if (old != target) _apps.LogStatus(app.Id, old, target, actor.Id, note);
    }

    public OperationResult AdminSetStatus(User actor, Application app, DocumentStatus target, string note)
    {
        if (actor is not Admin) return OperationResult.Fail("Bu işlem için yetkiniz yok.");
        var old = app.ChangeStatus(target, actor);
        if (old == target) return OperationResult.Success("Statü zaten bu durumda.");
        if (!string.IsNullOrWhiteSpace(note)) app.AdminNote = note;
        _apps.Save(app);
        _apps.LogStatus(app.Id, old, target, actor.Id, string.IsNullOrWhiteSpace(note) ? "Yönetici tarafından değiştirildi" : note);
        return OperationResult.Success($"Statü '{old.Label()}' → '{target.Label()}' olarak güncellendi.");
    }

    public OperationResult Delete(User actor, Application app)
    {
        if (!actor.CanModify(app)) return OperationResult.Fail("Tamamlandı statüsündeki belgeler silinemez.");
        _apps.Delete(app.Id);
        var folder = Path.Combine(_uploadFolder, app.Id.ToString());
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        return OperationResult.Success("Başvuru silindi.");
    }

    public string FilePath(Application app, Document doc) => Path.Combine(_uploadFolder, app.Id.ToString(), doc.StoredName);
}

public sealed class DatasetService
{
    private readonly DatasetRepository _repo;
    private readonly UploadValidationPipeline _pipeline;
    private readonly string _archiveFolder;

    public DatasetService(DatasetRepository repo, UploadValidationPipeline pipeline, string archiveFolder)
    {
        _repo = repo;
        _pipeline = pipeline;
        _archiveFolder = archiveFolder;
    }

    public (OperationResult Result, ImportResult Import) ImportFile(UploadedFile file, bool replace)
    {
        var check = _pipeline.Run(file);
        if (!check.Ok) return (OperationResult.Fail(check.Errors[0]), null);

        var import = ImporterFactory.ForExtension(file.Extension).Import(file.Content);
        if (!import.Ok)
            return (OperationResult.Fail(import.Errors.FirstOrDefault() ?? "Geçerli satır bulunamadı."), import);

        Directory.CreateDirectory(_archiveFolder);
        File.WriteAllBytes(Path.Combine(_archiveFolder, $"{Guid.NewGuid().ToString("N")[..8]}_{file.FileName}"), file.Content);
        if (replace) _repo.Clear();
        var (inserted, updated) = _repo.UpsertMany(import.Records, file.FileName);
        var msg = $"{inserted} yeni kayıt eklendi, {updated} kayıt güncellendi.";
        if (import.Errors.Count > 0) msg += $" {import.Errors.Count} satır hatalı olduğu için atlandı.";
        return (OperationResult.Success(msg), import);
    }
}

/// <summary>Bağımlılıkların tek noktada oluşturulduğu servis kabı</summary>
public sealed class ServiceContainer
{
    private readonly AppSettings _settings;

    public ServiceContainer(AppSettings settings)
    {
        _settings = settings;
        var factory = new AppDbContextFactory(settings.DatabasePath);
        DbFactory = factory;
        Users = new UserRepository(factory);
        Sessions = new SessionRepository(factory);
        Attempts = new LoginAttemptRepository(factory);
        Dataset = new DatasetRepository(factory);
        Applications = new ApplicationRepository(factory);
        Documents = new DocumentRepository(factory);

        Auth = new AuthService(Users, Sessions, Attempts, new Pbkdf2PasswordHasher(),
            settings.MaxFailedLogins, settings.LockMinutes, settings.SessionHours);
        ApplicationService = new ApplicationService(Applications, Documents, Dataset, settings.UploadFolder,
            UploadValidationPipeline.ForDocuments(AppSettings.AllowedDocumentExtensions, settings.MaxDocumentBytes));
        DatasetService = new DatasetService(Dataset,
            UploadValidationPipeline.ForDatasets(AppSettings.AllowedDatasetExtensions, settings.MaxDocumentBytes),
            settings.DatasetFolder);
    }

    public AppDbContextFactory DbFactory { get; }
    public UserRepository Users { get; }
    public SessionRepository Sessions { get; }
    public LoginAttemptRepository Attempts { get; }
    public DatasetRepository Dataset { get; }
    public ApplicationRepository Applications { get; }
    public DocumentRepository Documents { get; }
    public AuthService Auth { get; }
    public ApplicationService ApplicationService { get; }
    public DatasetService DatasetService { get; }

    /// <summary>
    /// Bekleyen migration'ları uygular (dotnet ef migrations add ... ile oluşturulanlar),
    /// hiç admin yoksa varsayılan admin hesabını ekler.
    /// </summary>
    public void Initialize()
    {
        using (var db = DbFactory.Create())
        {
            if (db.Database.GetMigrations().Any())
            {
                try
                {
                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Veritabanı migration ile güncellenemedi. Eski sürümden kalan App_Data/staj.db dosyası " +
                        "olabilir: uygulamayı durdurup App_Data klasörünü silin ve tekrar çalıştırın. Ayrıntı: " + ex.Message, ex);
                }
            }
            else
            {
                // Projede henüz migration yoksa tabloları modelden doğrudan oluştur
                Console.WriteLine("UYARI: Migration bulunamadı, tablolar EnsureCreated ile oluşturuluyor. " +
                                  "Migration için: dotnet ef migrations add IlkKurulum");
                db.Database.EnsureCreated();
            }
        }
        Directory.CreateDirectory(_settings.UploadFolder);
        if (Users.CountByRole(Role.Admin) == 0)
            Users.Create(_settings.DefaultAdminUsername, _settings.DefaultAdminEmail,
                Auth.Hasher.Hash(_settings.DefaultAdminPassword), Role.Admin, "Sistem Yöneticisi");
    }
}
