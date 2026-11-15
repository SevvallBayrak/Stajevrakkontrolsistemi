# Diyagramlar (Mermaid)

> Bu diyagramlar https://mermaid.live adresinde veya GitHub/VS Code (Mermaid eklentisi) ile görüntülenebilir;
> PNG olarak dışa aktarıp rapora ekleyebilirsiniz.

## 1. Sınıf Diyagramı (NYP prensipleri)

```mermaid
classDiagram
    direction LR
    class User {
        <<abstract>>
        -string _passwordHash
        -int _failedAttempts
        -DateTime? _lockedUntil
        +int Id
        +string Username
        +string Email
        +string FullName
        +IsLocked() bool
        +RegisterFailedAttempt(max, until) bool
        +ResetFailedAttempts()
        +ChangePasswordHash(hash)
        +Role Role*
        +MenuItem Home*
        +CanView(Application)* bool
        +CanModify(Application)* bool
        +MenuItems* IReadOnlyList~MenuItem~
    }
    class Student {
        +string StudentNo
        +string TcKimlikNo
        +CanModify(app) bool
    }
    class Admin {
        +CanModify(app) bool
    }
    User <|-- Student
    User <|-- Admin
    class UserFactory { +FromEntity(UserEntity) User }
    UserFactory ..> User

    class Application {
        -DocumentStatus _status
        +Dictionary FormData
        +List~string~ StajGunleri
        +string GoogleFormUrl
        +ChangeStatus(target, actor) DocumentStatus
        +Document CurrentDocument
    }
    class Document {
        +string OriginalName
        +string Extension
        +Dictionary ExtractedData
        +ComparisonReport Report
    }
    Application "1" o-- "*" Document
    Student "1" --> "*" Application

    class DocumentStatus {
        <<enumeration>>
        TASLAK
        YUKLENDI
        INCELENIYOR
        HATALI_EKSIK
        ONAYLANDI
        TAMAMLANDI
    }
    Application --> DocumentStatus

    class FileValidator {
        <<abstract>>
        +string Name*
        +Validate(UploadedFile)* string
    }
    FileValidator <|-- EmptyFileValidator
    FileValidator <|-- ExtensionValidator
    FileValidator <|-- SizeValidator
    FileValidator <|-- SignatureValidator
    class UploadValidationPipeline { -List~FileValidator~ _validators +Run(file) ValidationResult }
    UploadValidationPipeline o-- FileValidator

    class DocumentParser {
        <<abstract>>
        +Parse(byte[])* ParsedDocument
        #SegmentsFromRow()
        #SegmentsFromLine()
    }
    DocumentParser <|-- DocxParser
    DocumentParser <|-- PdfParser
    DocumentParser <|-- ImageParser
    DocumentParser <|-- LegacyDocParser
    class ParserFactory { +ForExtension(ext) DocumentParser }
    ParserFactory ..> DocumentParser
    class Ek16FieldExtractor { +Extract(parsed) Dictionary }

    class ValueComparer {
        <<abstract>>
        +Normalize(v)* string
        +AreEqual(a, b) bool
    }
    ValueComparer <|-- TextComparer
    ValueComparer <|-- DigitsComparer
    ValueComparer <|-- PhoneComparer
    ValueComparer <|-- EmailComparer
    ValueComparer <|-- DateComparer
    ValueComparer <|-- IntComparer
    class DocumentComparator { +Compare(form, parsed, extracted, dataset) ComparisonReport }
    DocumentComparator ..> ValueComparer

    class DatasetImporter {
        <<abstract>>
        #ReadRows(byte[])* List
        +Import(byte[]) ImportResult
    }
    DatasetImporter <|-- CsvDatasetImporter
    DatasetImporter <|-- XlsxDatasetImporter

    class BaseRepository~TEntity,TModel~ {
        <<abstract>>
        #AppDbContextFactory Factory
        #ToModel(TEntity)* TModel
        +Get(id) TModel
        +Count() int
        +Delete(id)
    }
    class AppDbContext {
        +DbSet Users, Applications, Documents ...
        +OnModelCreating(ModelBuilder)
    }
    DbContext <|-- AppDbContext
    BaseRepository ..> AppDbContext
    BaseRepository <|-- UserRepository
    BaseRepository <|-- ApplicationRepository
    BaseRepository <|-- DocumentRepository
    BaseRepository <|-- DatasetRepository

    class PasswordHasherBase { <<abstract>> +Hash(p)* +Verify(h, p)* }
    PasswordHasherBase <|-- Pbkdf2PasswordHasher
    class AuthService { +Login() +Logout() +RegisterStudent() +ResolveUser() }
    AuthService --> PasswordHasherBase
    AuthService --> UserRepository

    class ApplicationService { +Create() +Update() +Upload() +Analyze() +AdminSetStatus() +Delete() }
    ApplicationService --> UploadValidationPipeline
    ApplicationService --> ParserFactory
    ApplicationService --> Ek16FieldExtractor
    ApplicationService --> DocumentComparator
    ApplicationService --> ApplicationRepository

    class RoleRequiredAttribute { +OnAuthorization(ctx) }
    class AppController { <<abstract>> }
    AppController <|-- AuthController
    AppController <|-- StudentController
    AppController <|-- AdminController
    StudentController ..> RoleRequiredAttribute
    AdminController ..> RoleRequiredAttribute
```

## 2. Veritabanı (ER) Diyagramı

```mermaid
erDiagram
    USERS ||--o{ USER_SESSIONS : "oturum"
    USERS ||--o{ LOGIN_ATTEMPTS : "deneme"
    USERS ||--o{ APPLICATIONS : "başvurur"
    APPLICATIONS ||--o{ DOCUMENTS : "sürüm"
    APPLICATIONS ||--o{ STATUS_HISTORY : "geçmiş"
    USERS ||--o{ STATUS_HISTORY : "değiştirir"
    DATASET_RECORDS |o..o| USERS : "TC / öğrenci no ile eşleşir"

    USERS {
        int id PK
        text username UK
        text email UK
        text password_hash
        text role "ROLE_STUDENT | ROLE_ADMIN"
        text full_name
        text student_no
        text tc_kimlik_no
        int failed_attempts
        text locked_until
    }
    USER_SESSIONS {
        text token PK
        int user_id FK
        text role
        text expires_at
        int revoked
    }
    LOGIN_ATTEMPTS {
        int id PK
        text identifier
        int user_id FK
        text ip_address
        int success
        text reason
        text created_at
    }
    DATASET_RECORDS {
        int id PK
        text ad_soyad
        text tc_kimlik_no UK
        text uyruk
        text ogrenci_no UK
        text dogum_tarihi
        text ikametgah_adresi
        text bolum
        text teslim_yili
        int onceki_staj_gun
        text eposta
        text telefon
        text saglik_yardimi
    }
    APPLICATIONS {
        int id PK
        int student_id FK
        text status
        text form_data "JSON"
        text staj_gunleri "JSON"
        text google_form_url
        text admin_note
        text completed_at
    }
    DOCUMENTS {
        int id PK
        int application_id FK
        text original_name
        text stored_name
        text extension
        int size_bytes
        text extracted_data "JSON"
        text report "JSON"
        int is_current
    }
    STATUS_HISTORY {
        int id PK
        int application_id FK
        text old_status
        text new_status
        int changed_by FK
        text note
    }
```

## 3. Akış Diyagramı — Evrak yükleme ve kontrol

```mermaid
flowchart TD
    A([Öğrenci giriş yapar]) --> B[Ek-16 bilgilerini forma girer]
    B --> C{Form geçerli mi?}
    C -- Hayır --> B
    C -- Evet --> D[(Başvuru kaydedilir: TASLAK)]
    D --> E[Evrak Yükle]
    E --> F{Dosya boş mu?}
    F -- Evet --> X[Pop-up uyarı: dosya reddedildi]
    F -- Hayır --> G{Uzantı .doc/.docx/.pdf/.jpg/.jpeg/.png mi?}
    G -- Hayır --> X
    G -- Evet --> H{Boyut ≤ 10 MB?}
    H -- Hayır --> X
    H -- Evet --> I{İçerik imzası uzantıyla uyumlu mu?}
    I -- Hayır --> X
    X --> E
    I -- Evet --> J[(Dosya saklanır → YÜKLENDİ)]
    J --> K[İNCELENİYOR: uzantıya göre okuyucu seçilir]
    K --> L{Belge okunabildi mi?}
    L -- Hayır --> M[Manuel inceleme — İNCELENİYOR'da kalır]
    L -- Evet --> N[Alanlar çıkarılır ve girilen bilgilerle karşılaştırılır]
    N --> O[Veri seti kaydı ile karşılaştırılır]
    O --> P{Uyuşmazlık / eksik var mı?}
    P -- Evet --> Q[HATALI / EKSİK + rapor]
    Q --> E
    P -- Hayır --> R[ONAYLANDI — teslime hazır]
    R --> S[Admin: TAMAMLANDI]
    S --> T([Öğrenci artık güncelleyemez / silemez])
```

## 4. Akış — Oturum açma ve yetkilendirme

```mermaid
flowchart TD
    A([Giriş formu]) --> B{Kullanıcı var mı?}
    B -- Hayır --> L1[Log: başarısız] --> E[Hata mesajı]
    B -- Evet --> C{Hesap kilitli mi?}
    C -- Evet --> L2[Log: kilitli hesap] --> E
    C -- Hayır --> D{Şifre hash'i doğru mu?}
    D -- Hayır --> F[failed_attempts++] --> G{≥ 5 ?}
    G -- Evet --> H[Hesap 15 dk kilitlenir] --> E
    G -- Hayır --> E
    D -- Evet --> I[Sayaç sıfırlanır, token üretilir, rol oturuma yazılır]
    I --> J{Rol}
    J -- ROLE_STUDENT --> K[Öğrenci paneli]
    J -- ROLE_ADMIN --> M[Admin paneli]
    K & M --> N[Her istekte token + rol doğrulanır]
    N -- Yetkisiz --> O[403 → giriş sayfasına yönlendirme]
    N --> P[Çıkış: token iptal edilir]
```

## 5. Durum (Statü) Diyagramı

```mermaid
stateDiagram-v2
    [*] --> Taslak: form kaydedildi
    Taslak --> Yuklendi: evrak yüklendi
    Yuklendi --> Inceleniyor: analiz
    Inceleniyor --> HataliEksik: uyuşmazlık / eksik
    Inceleniyor --> Onaylandi: kontrol başarılı
    HataliEksik --> Yuklendi: düzeltilip yeniden yüklendi
    HataliEksik --> Inceleniyor: form güncellendi
    Onaylandi --> Tamamlandi: admin onayı
    Tamamlandi --> [*]
    note right of Tamamlandi: Öğrenci değiştiremez/silemez.\nYalnızca admin güncelleyebilir/silebilir.
```

## 6. UML Kullanım Durumu (Use Case)

```mermaid
flowchart LR
    S((Öğrenci))
    A((Admin))
    subgraph Sistem
      U1[Hesap oluştur]
      U2[Giriş / Çıkış]
      U3[Ek-16 bilgilerini gir]
      U4[Staj günlerini seç]
      U5[Evrak yükle]
      U6[Kontrol raporunu görüntüle]
      U7[Google Form URL gir]
      U8[Veri seti yükle]
      U9[Başvuruları listele / filtrele]
      U10[Statü değiştir / Tamamla]
      U11[Tamamlanan belgeyi güncelle / sil]
      U12[Kilitli hesabı aç / logları incele]
      U13[Ek-17 oluştur — final]
    end
    S --- U1 & U2 & U3 & U4 & U5 & U6 & U7
    A --- U2 & U8 & U9 & U10 & U11 & U12 & U13
```
