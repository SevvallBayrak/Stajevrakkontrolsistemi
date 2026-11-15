using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using StajEvrak.Core;

namespace StajEvrak.Web;

/// <summary>
/// Her istekte oturum çerezindeki token'ı doğrular ve kullanıcıyı HttpContext.Items içine koyar.
/// Token sunucu tarafında saklandığından çıkış yapıldığında çerez kopyalansa bile geçersizdir.
/// </summary>
public sealed class CurrentUserMiddleware
{
    public const string CookieName = "staj_oturum";
    public const string ItemKey = "CurrentUser";

    private readonly RequestDelegate _next;

    public CurrentUserMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ServiceContainer services)
    {
        var token = context.Request.Cookies[CookieName];
        if (!string.IsNullOrEmpty(token))
        {
            var user = services.Auth.ResolveUser(token);
            if (user != null) context.Items[ItemKey] = user;
            else context.Response.Cookies.Delete(CookieName);
        }
        await _next(context);
    }
}

public static class HttpContextExtensions
{
    public static User CurrentUser(this HttpContext context) =>
        context.Items.TryGetValue(CurrentUserMiddleware.ItemKey, out var u) ? u as User : null;
}

/// <summary>
/// Yetkilendirme katmanı (Bölüm 4.2): isteği yapan kullanıcının rolünü doğrular.
/// Yetkisiz istekler 403 döner ve login sayfasına yönlendiren sayfa gösterilir.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RoleRequiredAttribute : Attribute, IAuthorizationFilter
{
    private readonly Role _role;

    public RoleRequiredAttribute(Role role) => _role = role;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.CurrentUser();
        if (user == null || user.Role != _role)
            context.Result = ForbiddenResult(context.ModelState);
    }

    public static ViewResult ForbiddenResult(ModelStateDictionary modelState)
    {
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState) { ["Bare"] = true };
        return new ViewResult { ViewName = "Forbidden", ViewData = viewData, StatusCode = StatusCodes.Status403Forbidden };
    }
}

public sealed class FlashMessage
{
    public string Category { get; set; }
    public string Message { get; set; }
}

/// <summary>Bir sonraki sayfada gösterilecek bildirimler (TempData üzerinden).</summary>
public static class FlashExtensions
{
    private const string Key = "flashes";

    public static void Flash(this Controller controller, string category, string message)
    {
        var list = controller.TempData.Peek(Key) is string json
            ? JsonSerializer.Deserialize<List<FlashMessage>>(json) ?? new List<FlashMessage>()
            : new List<FlashMessage>();
        list.Add(new FlashMessage { Category = category, Message = message });
        controller.TempData[Key] = JsonSerializer.Serialize(list);
    }

    public static List<FlashMessage> ReadFlashes(this ITempDataDictionary tempData) =>
        tempData[Key] is string json
            ? JsonSerializer.Deserialize<List<FlashMessage>>(json) ?? new List<FlashMessage>()
            : new List<FlashMessage>();
}

/// <summary>Tüm controller'lar için ortak taban sınıf.</summary>
public abstract class AppController : Controller
{
    protected ServiceContainer Services => HttpContext.RequestServices.GetRequiredService<ServiceContainer>();

    /// <summary>Giriş yapmış kullanıcı (Controller.User → ClaimsPrincipal ile karışmaması için ayrı ad).</summary>
    protected User CurrentAccount => HttpContext.CurrentUser();

    protected IActionResult RedirectHome(User user) => RedirectToAction(user.Home.Action, user.Home.Controller);

    protected IActionResult Forbidden() => RoleRequiredAttribute.ForbiddenResult(ModelState);
}
