# Staj Evrak Doğrulama ve Yönetim Sistemi — Vize Sürümü (C#)

Programlama Laboratuvarı III · 2026-2027 Güz · Ek-16 belgesini yükleme, okuma ve doğrulama sistemi.

**Teknoloji:** C# · ASP.NET Core MVC (.NET 10) · Entity Framework Core + SQLite · DocumentFormat.OpenXml (Word/Excel) · PdfPig (PDF)

## Kurulum (Mac)

1. .NET 10 SDK'yı kurun:
   - https://dotnet.microsoft.com/download/dotnet/10.0 adresinden macOS **Installer**'ı indirin. Apple Silicon (M1/M2/M3) için **Arm64**, Intel Mac için **x64** sürümünü seçin.
   - Kurulumu kontrol etmek için Terminal'i yeniden açıp `dotnet --version` yazın. `10.x.x` görmelisiniz.
2. Proje klasörüne girip çalıştırın:
   ```bash
   cd ~/Downloads/StajEvrak
   dotnet run
   ```
   İlk çalıştırmada NuGet paketleri otomatik indirilir (internet gerekir).
3. Tarayıcıda **http://localhost:5080** adresini açın.

Windows'ta kurulum aynıdır; .NET 10 SDK'yı kurup proje klasöründe `dotnet run` yazmanız yeterli. Visual Studio veya Rider ile `StajEvrak.csproj` dosyasını açıp **Run** da diyebilirsiniz.

## Veritabanı ve migration (Entity Framework Core)

Tablolar `Data/AppDbContext.cs` içinde tanımlıdır. Şema migration ile oluşturulur:

```bash
dotnet tool install --global dotnet-ef        # bir kez (EF komut aracı)
export PATH="$PATH:$HOME/.dotnet/tools"       # Mac'te 'dotnet ef' bulunamazsa
dotnet ef migrations add IlkKurulum           # Migrations/ klasörünü oluşturur
dotnet run                                    # açılışta Migrate() ile tablolar oluşturulur
```

Modelde değişiklik yaptığınızda yeni bir migration ekleyin (`dotnet ef migrations add AlanEklendi`); uygulama açılırken otomatik uygulanır. Projede hiç migration yoksa uygulama tabloları `EnsureCreated()` ile oluşturur ve konsola uyarı yazar.

> Eski (EF öncesi) sürümden kalan `App_Data` klasörü varsa silin; aksi hâlde migration "tablo zaten var" hatası verir.

İlk çalıştırmada veritabanı (`App_Data/staj.db`) ve varsayılan admin hesabı oluşturulur:

| Rol   | Kullanıcı adı | Şifre       |
|-------|---------------|-------------|
| Admin | `admin`       | `Admin123!` |

Öğrenciler giriş sayfasındaki **"Öğrenci hesabı oluşturun"** bağlantısıyla hesaplarını kendileri açar ve şifrelerini kendileri belirler (soru-cevap #4).

## Hızlı deneme senaryosu (elle) (`sample_data/` klasörü)

1. `admin` ile giriş yapın → **Veri Seti** → `ornek_veri_seti.xlsx` (veya `.csv`) dosyasını yükleyin.
2. Çıkış yapıp öğrenci hesabı açın: Ad Soyad **Ayşe Yılmaz**, öğrenci no `230201001`, TC no `20433218148`.
3. **Yeni Başvuru** açın; kimlik bilgileri ve veri setindeki bilgiler forma otomatik gelir. Kalan alanları şöyle doldurun:
   - Öğretim yılı `2025-2026`, yarıyıl `Bahar`
   - Staj 06.07.2026 – 14.08.2026, `29` gün
   - Firma `Kırmızı Yazılım A.Ş.`, adres `Sanayi Mah., Teknopark Cad., No:1, D:12, İzmit/Kocaeli`
   - Firma telefonu `+90 262 555 10 20`, e-posta `ik@kirmiziyazilim.com.tr`
   - Mühendis sayısı `7`, yetkili `Hakan Yıldız`, unvan `İK Müdürü`
4. **Evrak Yükle** ile şu dosyaları deneyin:
   - `reddedilecek_dosya.txt` → pop-up ile reddedilir.
   - `sahte_pdf.pdf` → içerik imzası uyuşmadığı için sunucuda reddedilir.
   - `ornek_ek16_hatali.docx` → **Hatalı / Eksik** olur. Raporda telefon ve gün sayısı uyuşmazlığı ile boş alanlar görünür.
   - `ornek_ek16_uyumlu.docx` veya `ornek_ek16_uyumlu.pdf` → **Onaylandı** (teslime hazır).
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

Sınıf, ER, akış, durum ve kullanım durumu diyagramları `docs/diyagramlar.md` dosyasında Mermaid formatındadır. https://mermaid.live adresinden PNG olarak alabilirsiniz.

## Proje yapısı

```
Program.cs                 Uygulama başlangıcı, middleware ve servis kayıtları
appsettings.json           Ayarlar (port, kilit süresi, varsayılan admin)
Data/
  Entities.cs              EF Core varlık sınıfları (tablolar)
  AppDbContext.cs          DbContext, ilişkiler, snake_case sütun adları, tasarım zamanı fabrikası
Migrations/                'dotnet ef migrations add' ile oluşturulur
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
sample_data/               Örnek veri seti ve Ek-16 belgeleri
docs/diyagramlar.md        Diyagramlar
```

## Notlar

- **Ek-16 etiketleri:** Gerçek Ek-16 şablonundaki başlıklar farklıysa yalnızca `Core/Ek16Schema.cs` içindeki `Aliases` listelerini güncellemeniz yeterlidir.
- **Final aşamasına bırakılanlar:**
  - El yazısı OCR'ı ve 6. bölümdeki doğrulama kuralları. Görüntü dosyaları ve taranmış PDF'ler şimdilik "manuel inceleme" olarak işaretlenir.
  - Google Form'a otomatik kayıt ve Ek-17 üretimi. Ek-17 butonu arayüzde var ama pasif durumdadır.
- **Resmi tatiller:** Dini bayram tarihleri `Core/CalendarTr.cs` dosyasındadır; Diyanet takvimine göre kontrol edilmelidir.
- **Port:** Uygulama 5080 portunda çalışır. Mac'te 5000 portu AirPlay tarafından kullanıldığı için bu port seçildi. Değiştirmek için `appsettings.json` ve `Properties/launchSettings.json` dosyalarını düzenleyin.
