using Microsoft.AspNetCore.Mvc;
using StajEvrak.Core;
using StajEvrak.Web;

namespace StajEvrak.Controllers;

/// <summary>Öğrenci ekranları (ROLE_STUDENT).</summary>
[Route("ogrenci")]
[RoleRequired(Role.Student)]
public class StudentController : AppController
{
    private Student Me => (Student)CurrentAccount;

    /// <summary>Başvuruyu getirir; başkasına aitse null + 403 sonucu döner.</summary>
    private (Application App, IActionResult Error) Own(int appId)
    {
        var app = Services.Applications.Get(appId);
        if (app == null) return (null, NotFound());
        if (!Me.CanView(app)) return (null, Forbidden());
        return (app, null);
    }

    private Dictionary<string, string> Prefill()
    {
        var data = new Dictionary<string, string>
        {
            ["ad_soyad"] = Me.FullName, ["ogrenci_no"] = Me.StudentNo ?? "",
            ["tc_kimlik_no"] = Me.TcKimlikNo ?? "", ["eposta"] = Me.Email,
        };
        var rec = Services.Dataset.FindForStudent(Me.TcKimlikNo, Me.StudentNo);
        if (rec != null)
        {
            foreach (var key in new[] { "uyruk", "dogum_tarihi", "bolum", "ikametgah_adresi", "telefon", "saglik_yardimi" })
                if (!string.IsNullOrEmpty(rec.Get(key))) data[key] = rec.Get(key);
            if (!string.IsNullOrEmpty(rec.TeslimYili)) data["ogretim_yili"] = rec.TeslimYili;
        }
        return data;
    }

    [HttpGet("")]
    public IActionResult Dashboard()
    {
        var apps = Services.Applications.ForStudent(Me.Id);
        return View(new StudentDashboardVm
        {
            Applications = apps,
            Record = Services.Dataset.FindForStudent(Me.TcKimlikNo, Me.StudentNo),
            Total = apps.Count,
            Ready = apps.Count(a => a.Status is DocumentStatus.ONAYLANDI or DocumentStatus.TAMAMLANDI),
            Problem = apps.Count(a => a.Status == DocumentStatus.HATALI_EKSIK),
            Waiting = apps.Count(a => a.Status is DocumentStatus.TASLAK or DocumentStatus.YUKLENDI or DocumentStatus.INCELENIYOR),
        });
    }

    [HttpGet("basvuru/yeni")]
    public IActionResult NewApplication() =>
        View("ApplicationForm", new ApplicationFormVm
        {
            Data = Prefill(), Days = new List<string> { "0", "1", "2", "3", "4" }, IsNew = true
        });

    [HttpPost("basvuru/yeni")]
    public IActionResult NewApplication(IFormCollection form)
    {
        var parsed = Services.ApplicationService.ParseForm(form);
        var result = Services.ApplicationService.Create(Me, parsed);
        if (result.Ok)
        {
            this.Flash("success", result.Message);
            return RedirectToAction(nameof(Detail), new { appId = (int)result.Payload });
        }
        foreach (var e in parsed.Errors) this.Flash("error", e);
        return View("ApplicationForm", new ApplicationFormVm
        {
            Data = parsed.Data, Days = parsed.Days, Url = parsed.Url, UrlError = parsed.UrlError, IsNew = true
        });
    }

    [HttpGet("basvuru/{appId:int}")]
    public IActionResult Detail(int appId)
    {
        var (app, error) = Own(appId);
        if (error != null) return error;
        return View(new ApplicationDetailVm
        {
            Application = app,
            History = Services.Applications.History(app.Id),
            CanModify = Me.CanModify(app),
        });
    }

    [HttpGet("basvuru/{appId:int}/duzenle")]
    public IActionResult Edit(int appId)
    {
        var (app, error) = Own(appId);
        if (error != null) return error;
        if (!Me.CanModify(app))
        {
            this.Flash("error", "Tamamlandı statüsündeki belgeler güncellenemez veya silinemez.");
            return RedirectToAction(nameof(Detail), new { appId });
        }
        return View("ApplicationForm", new ApplicationFormVm
        {
            Data = app.FormData, Days = app.StajGunleri, Url = app.GoogleFormUrl, Application = app
        });
    }

    [HttpPost("basvuru/{appId:int}/duzenle")]
    public IActionResult Edit(int appId, IFormCollection form)
    {
        var (app, error) = Own(appId);
        if (error != null) return error;
        var parsed = Services.ApplicationService.ParseForm(form);
        var result = Services.ApplicationService.Update(Me, app, parsed);
        if (result.Ok)
        {
            this.Flash("success", result.Message);
            return RedirectToAction(nameof(Detail), new { appId });
        }
        foreach (var e in result.Errors) this.Flash("error", e);
        if (!Me.CanModify(app)) return RedirectToAction(nameof(Detail), new { appId });
        return View("ApplicationForm", new ApplicationFormVm
        {
            Data = parsed.Data, Days = parsed.Days, Url = parsed.Url, UrlError = parsed.UrlError, Application = app
        });
    }

    [HttpPost("basvuru/{appId:int}/yukle")]
    public async Task<IActionResult> Upload(int appId, IFormFile document)
    {
        var (app, error) = Own(appId);
        if (error != null) return error;
        var file = await UploadedFile.FromFormFileAsync(document);
        var result = Services.ApplicationService.Upload(Me, app, file);
        if (result.Ok)
        {
            var report = ((Document)result.Payload).Report;
            if (report.Ready)
                this.Flash("success", "Evrak yüklendi. Belge bilgileri sisteme girdiğiniz bilgilerle uyumlu — teslime hazır.");
            else if (report.ManualReview)
                this.Flash("info", "Evrak yüklendi. Belge otomatik okunamadığı için manuel incelemeye alındı.");
            else
                this.Flash("warning", "Evrak yüklendi ancak kontrol raporunda düzeltilmesi gereken alanlar var.");
        }
        else
        {
            this.Flash("upload_error", result.Message);
        }
        return Redirect(Url.Action(nameof(Detail), new { appId }) + "#rapor");
    }

    [HttpPost("basvuru/{appId:int}/sil")]
    public IActionResult Delete(int appId)
    {
        var (app, error) = Own(appId);
        if (error != null) return error;
        var result = Services.ApplicationService.Delete(Me, app);
        this.Flash(result.Ok ? "success" : "error", result.Message);
        return result.Ok ? RedirectToAction(nameof(Dashboard)) : RedirectToAction(nameof(Detail), new { appId });
    }

    [HttpGet("basvuru/{appId:int}/belge/{docId:int}")]
    public IActionResult Download(int appId, int docId)
    {
        var (app, error) = Own(appId);
        if (error != null) return error;
        var doc = app.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc == null) return NotFound();
        return PhysicalFile(Services.ApplicationService.FilePath(app, doc), MimeTypes.For(doc.Extension));
    }

    [HttpPost("staj-gunu-hesapla")]
    public IActionResult CalculateDays(IFormCollection form)
    {
        var start = TextUtils.ParseDate(form["start"].ToString());
        var end = TextUtils.ParseDate(form["end"].ToString());
        var days = form["days"].Select(d => int.TryParse(d, out var n) ? n : -1).Where(n => n >= 0).ToList();
        if (!start.HasValue || !end.HasValue || end < start || days.Count == 0)
            return Json(new InternshipDayResult { Ok = false, Message = "Geçerli bir tarih aralığı ve en az bir gün seçin." });
        return Json(new TurkishHolidayCalendar().CountInternshipDays(start.Value, end.Value, days));
    }

    [HttpGet("profil")]
    public IActionResult Profile() => View(Services.Dataset.FindForStudent(Me.TcKimlikNo, Me.StudentNo));

    [HttpPost("profil")]
    public IActionResult Profile(IFormCollection form)
    {
        var errors = Services.Auth.ChangePassword(Me, form["current"].ToString(), form["new"].ToString(), form["confirm"].ToString());
        if (errors.Count == 0)
            this.Flash("success", "Şifreniz güncellendi.");
        foreach (var e in errors)
            this.Flash("error", e);
        return RedirectToAction(nameof(Profile));
    }
}

public static class MimeTypes
{
    public static string For(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".doc" => "application/msword",
        _ => "application/octet-stream"
    };
}
