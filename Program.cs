// Staj Evrak Doğrulama ve Yönetim Sistemi — uygulama giriş noktası.
// Çalıştırma:  dotnet run   →  http://localhost:5080
using Microsoft.AspNetCore.Mvc;
using StajEvrak.Core;
using StajEvrak.Web;

var builder = WebApplication.CreateBuilder(args);

// Ayarlar
var settings = builder.Configuration.GetSection("App").Get<AppSettings>() ?? new AppSettings();
settings.Resolve(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<ServiceContainer>();

// MVC + tüm POST isteklerinde CSRF (antiforgery) doğrulaması
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options =>
{
    options.FormFieldName = "csrf_token";
    options.HeaderName = "X-CSRF-Token";
});

var app = builder.Build();

// Veritabanı şeması + varsayılan admin
app.Services.GetRequiredService<ServiceContainer>().Initialize();

app.UseStatusCodePagesWithReExecute("/hata/{0}");
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();

app.Run();
