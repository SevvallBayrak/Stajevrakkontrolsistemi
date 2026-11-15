using Microsoft.AspNetCore.Mvc;
using StajEvrak.Web;

namespace StajEvrak.Controllers;

/// <summary>Oturum açma, kayıt ve çıkış.</summary>
public class AuthController : AppController
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        var user = CurrentAccount;
        return user == null ? RedirectToAction(nameof(Login)) : RedirectHome(user);
    }

    [HttpGet("giris")]
    public IActionResult Login()
    {
        if (CurrentAccount != null) return RedirectHome(CurrentAccount);
        ViewData["Identifier"] = "";
        return View();
    }

    [HttpPost("giris")]
    public IActionResult Login(IFormCollection form)
    {
        var identifier = form["identifier"].ToString();
        var result = Services.Auth.Login(identifier, form["password"].ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString());
        if (result.Success)
        {
            Response.Cookies.Append(CurrentUserMiddleware.CookieName, result.Token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = result.ExpiresAt,
                IsEssential = true,
            });
            this.Flash("success", result.Message);
            return RedirectHome(result.User);
        }
        this.Flash(result.Locked ? "locked" : "error", result.Message);
        ViewData["Identifier"] = identifier;
        return View();
    }

    [HttpGet("kayit")]
    public IActionResult Register()
    {
        if (CurrentAccount != null) return RedirectHome(CurrentAccount);
        return View(new Dictionary<string, string>());
    }

    [HttpPost("kayit")]
    public IActionResult Register(IFormCollection form)
    {
        var errors = Services.Auth.RegisterStudent(form);
        if (errors.Count == 0)
        {
            this.Flash("success", "Hesabınız oluşturuldu. Belirlediğiniz şifre ile giriş yapabilirsiniz.");
            return RedirectToAction(nameof(Login));
        }
        foreach (var e in errors) this.Flash("error", e);
        var values = form.Keys.Where(k => !k.StartsWith("password")).ToDictionary(k => k, k => form[k].ToString());
        return View(values);
    }

    [HttpPost("cikis")]
    public IActionResult Logout()
    {
        Services.Auth.Logout(Request.Cookies[CurrentUserMiddleware.CookieName]);
        Response.Cookies.Delete(CurrentUserMiddleware.CookieName);
        this.Flash("info", "Oturumunuz kapatıldı.");
        return RedirectToAction(nameof(Login));
    }

    // Durum kodu sayfaları (UseStatusCodePagesWithReExecute) — her HTTP metoduyla yeniden çalıştırılabilir
    [Route("hata/{code:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int code)
    {
        if (code == 403) return Forbidden();
        Response.StatusCode = code;
        ViewData["Code"] = code;
        if (code == 404)
        {
            ViewData["ErrorTitle"] = "Sayfa bulunamadı";
            ViewData["ErrorMessage"] = "Aradığınız sayfa mevcut değil veya taşınmış olabilir.";
        }
        else if (code == 400)
        {
            ViewData["ErrorTitle"] = "Geçersiz istek";
            ViewData["ErrorMessage"] = "Form anahtarı geçersiz veya süresi dolmuş olabilir. Sayfayı yenileyip tekrar deneyin.";
        }
        else
        {
            ViewData["ErrorTitle"] = "Bir hata oluştu";
            ViewData["ErrorMessage"] = "İsteğiniz işlenemedi.";
        }
        return View("Error");
    }
}
