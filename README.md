# Staj Evrak Doğrulama ve Yönetim Sistemi — Vize Sürümü (C#)

Programlama Laboratuvarı III · 2026-2027 Güz · Ek-16 belgesini yükleme, okuma ve doğrulama sistemi.

**Teknoloji:** C# · ASP.NET Core MVC (.NET 10) · Entity Framework Core + SQLite · DocumentFormat.OpenXml (Word/Excel) · PdfPig (PDF)

## Kurulum (Mac)

1. .NET 10 SDK'yı kurun:
   - https://dotnet.microsoft.com/download/dotnet/10.0 adresinden macOS **Installer**'ı indirin. Apple Silicon (M1/M2/M3) için **Arm64**, Intel Mac için **x64** sürümünü seçin.
   - Kurulumu kontrol etmek için Terminal'i yeniden açıp `dotnet --version` yazın. `10.x.x` görmelisiniz.
2. Proje klasörüne girip çalıştırın:
   ```bash
   cd <proje-klasörü>
   dotnet run
   ```
   İlk çalıştırmada NuGet paketleri otomatik indirilir (internet gerekir).
3. Tarayıcıda **http://localhost:5080** adresini açın.

Windows'ta kurulum aynıdır; .NET 10 SDK'yı kurup proje klasöründe `dotnet run` yazmanız yeterli. Visual Studio veya Rider ile `StajEvrak.csproj` dosyasını açıp **Run** da diyebilirsiniz.

## Veritabanı ve migration (Entity Framework Core)

Tablolar `Data/AppDbContext.cs` içinde tanımlıdır. Şema, `Migrations/` klasöründeki `IlkKurulum` migration'ı ile oluşturulur. Uygulama açılırken `Migrate()` çağrılır ve bekleyen migration'lar otomatik uygulanır; ayrıca bir komut çalıştırmak gerekmez.

Modelde değişiklik yapıldığında yeni bir migration eklenir:

```bash
dotnet tool install --global dotnet-ef        # bir kez (EF komut aracı)
export PATH="$PATH:$HOME/.dotnet/tools"       # Mac'te 'dotnet ef' bulunamazsa
dotnet ef migrations add AlanEklendi          # yeni migration
dotnet run                                    # açılışta otomatik uygulanır
```

İlk çalıştırmada veritabanı (`App_Data/staj.db`) ve varsayılan admin hesabı oluşturulur:

| Rol   | Kullanıcı adı | Şifre       |
|-------|---------------|-------------|
| Admin | `admin`       | `Admin123!` |

Öğrenciler giriş sayfasındaki **"Öğrenci hesabı oluşturun"** bağlantısıyla hesaplarını kendileri açar ve şifrelerini kendileri belirler (soru-cevap #4).

## Hızlı deneme senaryosu

1. `admin` ile giriş yapın → **Veri Seti** → öğrenci bilgilerini içeren `.xlsx` veya `.csv` dosyasını yükleyin.
2. Çıkış yapıp giriş sayfasından öğrenci hesabı açın. Ad soyad, öğrenci no ve TC kimlik no veri setindeki bir kayıtla aynı olmalıdır.
3. **Yeni Başvuru** açın. Kimlik bilgileri ve veri setindeki bilgiler forma otomatik gelir; staj ve işyeri bilgilerini doldurup kaydedin.
4. **Evrak Yükle** ile deneyin:
   - İzin verilmeyen uzantıdaki bir dosya (ör. `.txt`) → pop-up ile reddedilir.
   - Uzantısı `.pdf` olup içeriği PDF olmayan bir dosya → içerik imzası uyuşmadığı için sunucuda reddedilir.
   - Bilgileri formla uyuşmayan bir Ek-16 → **Hatalı / Eksik** olur; raporda uyuşmayan ve boş alanlar listelenir.
   - Bilgileri formla aynı olan bir Ek-16 (`.docx` veya `.pdf`) → **Onaylandı** (teslime hazır).
5. Admin olarak başvuru detayına girip **Tamamlandı yap** deyin. Öğrenci artık belgeyi düzenleyemez ve silemez; admin hâlâ düzenleyip silebilir.

## Vize isterleri → karşılığı

| İster | Nerede |
|---|---|
| Kullanıcı adı / e-posta + şifre ile giriş | `Core/Security.cs → AuthService.Login` |
| Şifrelerin hash'lenmesi | `Pbkdf2PasswordHasher` (PBKDF2-SHA256, rastgele tuz) |
| Başarısız girişlerin loglanması, 5 hatada kilit | `login_attempts` tablosu, `User.RegisterFailedAttempt`; Admin → Giriş Kayıtları / Kullanıcılar (kilidi açma) |
| Rolün oturumda taşınması, çıkışta oturumun geçersiz kılınması | `user_sessions` tablosu (sunucu tarafı token, çıkışta `revoked = 1`), `CurrentUserMiddleware` |
| ROLE_STUDENT / ROLE_ADMIN; yetkisiz isteğe 403 ve login'e yönlendirme | `[RoleRequired]` filtresi + `Views/Shared/Forbidden.cshtml` |
| Admin ve öğrenci sayfaları | `Controllers/AdminController.cs`, `Controllers/StudentController.cs` |
| Admin veri seti yükleme (CSV/XLSX) | `Core/Importers.cs`, Admin → Veri Seti |
| Evrak yükleme ve format/uzantı kontrolleri | `Core/Validators.cs`: boş dosya, uzantı, boyut, içerik imzası; istemci tarafında da pop-up |
| Girilen bilgiler ile belgeden okunanların karşılaştırılması (S-C #1) | `Core/Parsers.cs` + `Core/Comparison.cs` |
| Ek-16 ↔ veri seti karşılaştırması (Bölüm 4.3) | `DocumentComparator.CompareDataset` |
| Tamamlandı belgeyi öğrenci değiştiremez/silemez, admin yapabilir (S-C #3) | `Student.CanModify` / `Admin.CanModify` |
| CSRF koruması | ASP.NET Core Antiforgery (tüm POST isteklerinde) |

## NYP prensipleri

- **Kapsülleme:** `User` sınıfındaki `_passwordHash`, `_failedAttempts`, `_lockedUntil` ile `Application._status` private alanlardır. Yalnızca `RegisterFailedAttempt()`, `ChangeStatus()` gibi kontrollü metotlarla değiştirilir. `AppDbContextFactory` bağlantı ayarlarını dışarıya açmaz.
- **Soyutlama:** `User`, `FileValidator`, `DocumentParser`, `DatasetImporter`, `ValueComparer`, `PasswordHasherBase`, `BaseRepository<TEntity, TModel>` ve `AppController` soyut sınıflardır.
- **Kalıtım:**
  - `Student` ve `Admin` ← `User`
  - 4 okuyucu ← `DocumentParser`
  - 4 kontrol ← `FileValidator`
  - 4 repository ← `BaseRepository<TEntity, TModel>`
  - `AppDbContext` ← EF Core `DbContext`
  - 6 karşılaştırıcı ← `ValueComparer`
  - 3 controller ← `AppController`
- **Çok biçimlilik:**
  - `user.CanModify(app)`, `user.Home` ve `user.MenuItems` role göre farklı çalışır.
  - `ParserFactory.ForExtension(ext).Parse()` dosya türüne göre doğru okuyucuyu çağırır.
  - `pipeline.Run()` her `validator.Validate()` metodunu aynı arayüzden çağırır.

## Proje yapısı

```
Program.cs                 Uygulama başlangıcı, middleware ve servis kayıtları
appsettings.json           Ayarlar (port, kilit süresi, varsayılan admin)
Data/
  Entities.cs              EF Core varlık sınıfları (tablolar)
  AppDbContext.cs          DbContext, ilişkiler, snake_case sütun adları, tasarım zamanı fabrikası
Migrations/                EF Core migration'ları (IlkKurulum)
Core/
  Enums.cs                 Role, DocumentStatus (statü akışı)
  Models.cs                User/Student/Admin, Application, Document, DatasetRecord
  Repositories.cs          Veri erişim katmanı (EF Core + LINQ)
  Security.cs              Şifre hash, AuthService, kayıt doğrulama
  Validators.cs            Dosya yükleme kontrolleri
  Parsers.cs               Belge okuma (docx, pdf, görüntü, doc) + alan çıkarımı
  Comparison.cs            Karşılaştırma raporu
  Importers.cs             Veri seti içe aktarma (CSV, XLSX)
  CalendarTr.cs            Staj günü hesaplama (resmi tatiller)
  Ek16Schema.cs            Ek-16 alan tanımları ve belgedeki etiket varyasyonları
  Services.cs              İş mantığı + ServiceContainer
Web/                       Middleware, yetki filtresi, bildirimler, view modelleri, HTML yardımcıları
Controllers/               Auth, Student, Admin
Views/                     Razor sayfaları (kırmızı-siyah-beyaz tema)
wwwroot/                   CSS ve JavaScript
```

## Notlar

- **Ek-16 etiketleri:** Gerçek Ek-16 şablonundaki başlıklar farklıysa yalnızca `Core/Ek16Schema.cs` içindeki `Aliases` listelerini güncellemeniz yeterlidir.
- **Final aşamasına bırakılanlar:**
  - El yazısı OCR'ı ve 6. bölümdeki doğrulama kuralları. Görüntü dosyaları ve taranmış PDF'ler şimdilik "manuel inceleme" olarak işaretlenir.
  - Google Form'a otomatik kayıt ve Ek-17 üretimi. Ek-17 butonu arayüzde var ama pasif durumdadır.
- **Resmi tatiller:** Dini bayram tarihleri `Core/CalendarTr.cs` dosyasındadır; Diyanet takvimine göre kontrol edilmelidir.
- **Port:** Uygulama 5080 portunda çalışır. Mac'te 5000 portu AirPlay tarafından kullanıldığı için bu port seçildi. Değiştirmek için `appsettings.json` ve `Properties/launchSettings.json` dosyalarını düzenleyin.
