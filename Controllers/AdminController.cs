using System.Text;
using Microsoft.AspNetCore.Mvc;
using StajEvrak.Core;
using StajEvrak.Web;

namespace StajEvrak.Controllers;

/// <summary>Admin paneli (ROLE_ADMIN).</summary>
[Route("admin")]
[RoleRequired(Role.Admin)]
public class AdminController : AppController
{
    private Admin Me => (Admin)CurrentAccount;

    [HttpGet("")]
    public IActionResult Dashboard()
    {
        var counts = Services.Applications.StatusCounts();
        return View(new AdminDashboardVm
        {
            Counts = counts,
            Recent = Services.Applications.Search().Take(6).ToList(),
            Total = counts.Values.Sum(),
            DatasetCount = Services.Dataset.Count(),
            StudentCount = Services.Users.CountByRole(Role.Student),
            FailedToday = Services.Attempts.FailedCountToday(),
            LockedCount = Services.Users.CountLocked(DateTime.Now),
        });
    }

    [HttpGet("basvurular")]
    public IActionResult Applications(string statu, string q)
    {
        if (statu == DocumentStatus.TAMAMLANDI.ToString()) return RedirectToAction(nameof(Completed), new { q });
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        return View(new ApplicationListVm
        {
            Applications = Services.Applications.Search(statu, q, excludeCompleted: true),
            Status = statu,
            Query = q ?? "",
            Counts = Services.Applications.StatusCounts(),
        });
    }

    [HttpGet("tamamlandi")]
    public IActionResult Completed(string q)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        return View(new ApplicationListVm
        {
            Applications = Services.Applications.Search(DocumentStatus.TAMAMLANDI.ToString(), q),
            Query = q ?? "",
            Counts = Services.Applications.StatusCounts(),
        });
    }

    [HttpGet("basvuru/{appId:int}")]
    public IActionResult Detail(int appId)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return NotFound();
        return View(new ApplicationDetailVm
        {
            Application = app,
            History = Services.Applications.History(app.Id),
            CanModify = true,
            Record = Services.Dataset.FindForStudent(app.Field("tc_kimlik_no"), app.Field("ogrenci_no")),
        });
    }

    [HttpGet("basvuru/{appId:int}/duzenle")]
    public IActionResult Edit(int appId)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return NotFound();
        return View("ApplicationForm", new ApplicationFormVm
        {
            Data = app.FormData, Days = app.StajGunleri, Url = app.GoogleFormUrl, Application = app, AdminMode = true
        });
    }

    [HttpPost("basvuru/{appId:int}/duzenle")]
    public IActionResult Edit(int appId, IFormCollection form)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return NotFound();
        var parsed = Services.ApplicationService.ParseForm(form);
        var result = Services.ApplicationService.Update(Me, app, parsed);
        if (result.Ok)
        {
            this.Flash("success", result.Message);
            return RedirectToAction(nameof(Detail), new { appId });
        }
        foreach (var e in result.Errors) this.Flash("error", e);
        return View("ApplicationForm", new ApplicationFormVm
        {
            Data = parsed.Data, Days = parsed.Days, Url = parsed.Url, UrlError = parsed.UrlError,
            Application = app, AdminMode = true
        });
    }

    [HttpPost("basvuru/{appId:int}/statu")]
    public IActionResult ChangeStatus(int appId, string status, string note)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return NotFound();
        if (!DocumentStatusExtensions.TryParseStatus(status, out var target)) return BadRequest();
        var result = Services.ApplicationService.AdminSetStatus(Me, app, target, note?.Trim());
        this.Flash(result.Ok ? "success" : "error", result.Message);
        return RedirectToAction(nameof(Detail), new { appId });
    }

    [HttpPost("basvuru/{appId:int}/yukle")]
    public async Task<IActionResult> Upload(int appId, IFormFile document)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return NotFound();
        var result = Services.ApplicationService.Upload(Me, app, await UploadedFile.FromFormFileAsync(document));
        this.Flash(result.Ok ? "success" : "upload_error", result.Message);
        return RedirectToAction(nameof(Detail), new { appId });
    }

    [HttpPost("basvuru/{appId:int}/yeniden-analiz")]
    public IActionResult Reanalyze(int appId)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return NotFound();
        if (app.CurrentDocument == null)
        {
            this.Flash("error", "Analiz edilecek belge yok.");
        }
        else
        {
            Services.ApplicationService.Analyze(app, Me, "Yönetici yeniden analiz başlattı");
            this.Flash("success", "Belge yeniden analiz edildi.");
        }
        return RedirectToAction(nameof(Detail), new { appId });
    }

    [HttpPost("basvuru/{appId:int}/sil")]
    public IActionResult Delete(int appId)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return NotFound();
        var wasCompleted = app.Status == DocumentStatus.TAMAMLANDI;
        var result = Services.ApplicationService.Delete(Me, app);
        this.Flash(result.Ok ? "success" : "error", result.Message);
        return RedirectToAction(wasCompleted ? nameof(Completed) : nameof(Applications));
    }

    [HttpGet("basvuru/{appId:int}/belge/{docId:int}")]
    public IActionResult Download(int appId, int docId)
    {
        var app = Services.Applications.Get(appId);
        var doc = app?.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc == null) return NotFound();
        return PhysicalFile(Services.ApplicationService.FilePath(app, doc), MimeTypes.For(doc.Extension), doc.OriginalName);
    }

    // ---------------------------------------------------------------- Veri seti
    [HttpGet("veri-seti")]
    public IActionResult Dataset(string q) => View(BuildDatasetVm(q, null));

    [HttpPost("veri-seti")]
    public async Task<IActionResult> Dataset(IFormFile dataset, string replace)
    {
        var (op, import) = Services.DatasetService.ImportFile(await UploadedFile.FromFormFileAsync(dataset),
            !string.IsNullOrEmpty(replace));
        this.Flash(op.Ok ? "success" : "upload_error", op.Message);
        return View(BuildDatasetVm(null, import));
    }

    private DatasetVm BuildDatasetVm(string q, ImportResult import)
    {
        q = (q ?? "").Trim();
        return new DatasetVm
        {
            Records = q.Length > 0 ? Services.Dataset.Search(q) : Services.Dataset.AllOrdered(),
            Query = q,
            Import = import,
        };
    }

    [HttpPost("veri-seti/temizle")]
    public IActionResult ClearDataset()
    {
        Services.Dataset.Clear();
        this.Flash("info", "Veri seti temizlendi.");
        return RedirectToAction(nameof(Dataset));
    }

    [HttpGet("veri-seti/sablon.csv")]
    public IActionResult DatasetTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", Ek16Schema.DatasetKeys.Select(k => Ek16Schema.DatasetLabels[k])));
        sb.AppendLine("Ayşe Yılmaz;10000000146;T.C.;220201001;15.03.2003;Yeni Mah., Atatürk Cad., No:12, D:4, İzmit/Kocaeli;" +
                      "Yazılım Mühendisliği;2025-2026;20;ayse.yilmaz@example.com;+90 532 111 22 33;Anne/Baba üzerinden");
        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", "veri_seti_sablonu.csv");
    }

    //Kullanıcılar & loglar
    [HttpGet("kullanicilar")]
    public IActionResult Users() =>
        View(new UsersVm { Users = Services.Users.ListWithStats(), Now = DateTime.Now });

    [HttpPost("kullanicilar/{userId:int}/kilit-ac")]
    public IActionResult UnlockUser(int userId)
    {
        var user = Services.Users.Get(userId);
        if (user == null) return NotFound();
        Services.Auth.Unlock(user);
        this.Flash("success", $"{user.FullName} kullanıcısının kilidi açıldı.");
        return RedirectToAction(nameof(Users));
    }

    [HttpGet("giris-kayitlari")]
    public IActionResult LoginLogs(string hatali)
    {
        var onlyFailed = hatali == "1";
        return View(new LoginLogsVm { Logs = Services.Attempts.Recent(300, onlyFailed), OnlyFailed = onlyFailed });
    }
}
