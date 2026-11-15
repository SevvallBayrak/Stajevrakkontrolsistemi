namespace StajEvrak.Core;

/// <summary>
/// Ek-16 (Staj Başvuru Formu) alan tanımı.
///  Kind    : karşılaştırma türü (text, date, phone, digits, email, int)
///  Aliases : belge içinde bu alanı tanımlayan olası etiketler (otomatik okuma için)
///  SectionAliases : yalnızca kendi bölümü (ör. İşyeri) içinde geçerli genel etiketler
/// Gerçek Ek-16 şablonundaki etiketler farklıysa yalnızca Aliases listelerini güncellemek yeterlidir.
/// </summary>
public sealed class Ek16Field
{
    public string Key { get; init; }
    public string Label { get; init; }
    public string Group { get; init; }
    public string Kind { get; init; } = "text";
    public string[] Aliases { get; init; } = Array.Empty<string>();
    public string[] SectionAliases { get; init; } = Array.Empty<string>();
    public bool Required { get; init; } = true;
    public string InputType { get; init; } = "text";
    public string Placeholder { get; init; } = "";
    public string[] Choices { get; init; } = Array.Empty<string>();
}

public static class Ek16Schema
{
    public const string GroupStudent = "Öğrenci Bilgileri";
    public const string GroupInternship = "Staj Bilgileri";
    public const string GroupCompany = "İşyeri Bilgileri";

    public static readonly string[] Groups = { GroupStudent, GroupInternship, GroupCompany };

    public static readonly IReadOnlyList<Ek16Field> Fields = new List<Ek16Field>
    {
        // --- Öğrenci ---
        new() { Key = "ad_soyad", Label = "Adı Soyadı", Group = GroupStudent,
                Aliases = new[] { "adı soyadı", "ad soyad", "adı-soyadı", "öğrencinin adı soyadı" }, Placeholder = "Ayşe Yılmaz" },
        new() { Key = "tc_kimlik_no", Label = "T.C. Kimlik No", Group = GroupStudent, Kind = "digits",
                Aliases = new[] { "t.c. kimlik no", "tc kimlik no", "t.c. kimlik numarası", "kimlik no", "yabancı kimlik no" }, Placeholder = "11 haneli" },
        new() { Key = "uyruk", Label = "Uyruğu", Group = GroupStudent, Aliases = new[] { "uyruğu", "uyruk" },
                InputType = "select", Choices = new[] { "T.C.", "Yabancı" } },
        new() { Key = "ogrenci_no", Label = "Öğrenci No", Group = GroupStudent, Kind = "digits",
                Aliases = new[] { "öğrenci no", "öğrenci numarası", "okul no" } },
        new() { Key = "dogum_tarihi", Label = "Doğum Tarihi", Group = GroupStudent, Kind = "date",
                Aliases = new[] { "doğum tarihi", "doğum yeri ve tarihi" }, InputType = "date" },
        new() { Key = "bolum", Label = "Bölümü", Group = GroupStudent,
                Aliases = new[] { "bölümü", "bölüm", "programı" }, Placeholder = "Yazılım Mühendisliği" },
        new() { Key = "ogretim_yili", Label = "Öğretim Yılı", Group = GroupStudent,
                Aliases = new[] { "öğretim yılı", "eğitim öğretim yılı" }, Placeholder = "2025-2026" },
        new() { Key = "yariyil", Label = "Yarıyıl", Group = GroupStudent, Aliases = new[] { "yarıyıl", "dönem" },
                InputType = "select", Choices = new[] { "Güz", "Bahar", "Yaz" } },
        new() { Key = "telefon", Label = "Telefon", Group = GroupStudent, Kind = "phone",
                Aliases = new[] { "telefon", "telefon no", "cep telefonu", "gsm", "öğrenci telefon" }, Placeholder = "+90 5xx xxx xx xx" },
        new() { Key = "eposta", Label = "E-posta", Group = GroupStudent, Kind = "email",
                Aliases = new[] { "e-posta", "e-mail", "eposta", "email", "öğrenci e-posta" } },
        new() { Key = "ikametgah_adresi", Label = "İkametgâh Adresi", Group = GroupStudent,
                Aliases = new[] { "ikametgah adresi", "ikametgâh adresi", "ikamet adresi", "adres" },
                InputType = "textarea", Placeholder = "Mahalle, Cadde/Sokak, Bina No, Daire No, İlçe/İl" },
        new() { Key = "saglik_yardimi", Label = "Sağlık Yardımı Statüsü", Group = GroupStudent,
                Aliases = new[] { "sağlık yardımı", "sağlık yardımı statüsü", "sağlık güvencesi", "genel sağlık sigortası" },
                InputType = "select", Choices = new[] { "Anne/Baba üzerinden", "Kendi üzerinden (GSS)", "Yok" } },

        // --- Staj ---
        new() { Key = "staj_baslangic", Label = "Staj Başlangıç Tarihi", Group = GroupInternship, Kind = "date",
                Aliases = new[] { "staj başlangıç tarihi", "başlangıç tarihi", "stajın başlama tarihi" }, InputType = "date" },
        new() { Key = "staj_bitis", Label = "Staj Bitiş Tarihi", Group = GroupInternship, Kind = "date",
                Aliases = new[] { "staj bitiş tarihi", "bitiş tarihi", "stajın bitiş tarihi" }, InputType = "date" },
        new() { Key = "staj_gun_sayisi", Label = "Staj Gün Sayısı", Group = GroupInternship, Kind = "int",
                Aliases = new[] { "staj gün sayısı", "gün sayısı", "staj süresi", "toplam gün" }, InputType = "number" },

        // --- İşyeri ---
        new() { Key = "firma_adi", Label = "İşyeri / Firma Adı", Group = GroupCompany,
                Aliases = new[] { "firma adı", "işyeri adı", "kurum adı", "kurumun adı", "işyerinin adı" } },
        new() { Key = "firma_adresi", Label = "İşyeri Adresi", Group = GroupCompany,
                Aliases = new[] { "firma adresi", "işyeri adresi", "kurum adresi", "işyerinin adresi" },
                SectionAliases = new[] { "adres", "adresi" }, InputType = "textarea" },
        new() { Key = "firma_telefon", Label = "İşyeri Telefon", Group = GroupCompany, Kind = "phone",
                Aliases = new[] { "firma telefon", "işyeri telefon", "işyeri telefonu", "kurum telefonu" },
                SectionAliases = new[] { "telefon", "tel" }, Placeholder = "+90 xxx xxx xx xx" },
        new() { Key = "firma_faks", Label = "İşyeri Faks", Group = GroupCompany, Kind = "phone",
                Aliases = new[] { "faks", "fax", "işyeri faks" }, Required = false },
        new() { Key = "firma_eposta", Label = "İşyeri E-posta", Group = GroupCompany, Kind = "email",
                Aliases = new[] { "firma e-posta", "işyeri e-posta", "kurum e-posta", "işyeri e-mail" },
                SectionAliases = new[] { "e-posta", "e-mail", "eposta" } },
        new() { Key = "muhendis_sayisi", Label = "Mühendis Sayısı", Group = GroupCompany, Kind = "int",
                Aliases = new[] { "mühendis sayısı", "çalışan mühendis sayısı" }, InputType = "number" },
        new() { Key = "yetkili_ad_soyad", Label = "Yetkili Adı Soyadı", Group = GroupCompany,
                Aliases = new[] { "yetkili adı soyadı", "işveren adı soyadı", "onaylayan yetkili", "yetkilinin adı soyadı" },
                SectionAliases = new[] { "adı soyadı", "ad soyad" } },
        new() { Key = "yetkili_unvan", Label = "Yetkili Unvanı", Group = GroupCompany,
                Aliases = new[] { "unvanı", "yetkili unvanı", "görevi" } },
    };

    public static readonly Dictionary<string, Ek16Field> FieldsByKey = Fields.ToDictionary(f => f.Key);

    public static readonly (string Value, string Name)[] Weekdays =
    {
        ("0", "Pazartesi"), ("1", "Salı"), ("2", "Çarşamba"), ("3", "Perşembe"), ("4", "Cuma"), ("5", "Cumartesi")
    };

    public static string WeekdayName(string value) =>
        Weekdays.FirstOrDefault(w => w.Value == value).Name ?? value;

    // Veri seti sütunları (Bölüm 4.3) — Türkçe başlık varyasyonları kabul edilir
    public static readonly Dictionary<string, string[]> DatasetColumns = new()
    {
        ["ad_soyad"] = new[] { "ad soyad", "adı soyadı", "öğrenci ad soyad", "ad_soyad", "isim" },
        ["tc_kimlik_no"] = new[] { "tc kimlik no", "tc", "t.c. kimlik no", "tc_kimlik_no", "kimlik no" },
        ["uyruk"] = new[] { "uyruk", "uyruğu" },
        ["ogrenci_no"] = new[] { "öğrenci no", "ogrenci no", "ogrenci_no", "öğrenci numarası" },
        ["dogum_tarihi"] = new[] { "doğum tarihi", "dogum tarihi", "dogum_tarihi" },
        ["ikametgah_adresi"] = new[] { "ikametgah adresi", "ikametgâh adresi", "adres", "açık adres", "ikametgah_adresi" },
        ["bolum"] = new[] { "bölüm", "bolum", "okuduğu bölüm", "öğrencinin okuduğu bölüm" },
        ["teslim_yili"] = new[] { "teslim yılı", "teslim_yili", "ek-16 teslim yılı", "teslim ettiği yıl" },
        ["onceki_staj_gun"] = new[] { "daha önce yapılan staj gün sayısı", "önceki staj gün", "onceki_staj_gun", "staj gün sayısı", "yapılan staj günü" },
        ["eposta"] = new[] { "e-posta", "eposta", "email", "e-mail" },
        ["telefon"] = new[] { "telefon", "telefon no", "tel" },
        ["saglik_yardimi"] = new[] { "sağlık yardımı statüsü", "sağlık yardımı", "saglik_yardimi", "sağlık statüsü" },
    };

    public static readonly string[] DatasetRequired = { "ad_soyad", "tc_kimlik_no", "ogrenci_no" };

    public static readonly Dictionary<string, string> DatasetLabels = new()
    {
        ["ad_soyad"] = "Ad Soyad", ["tc_kimlik_no"] = "TC Kimlik No", ["uyruk"] = "Uyruk",
        ["ogrenci_no"] = "Öğrenci No", ["dogum_tarihi"] = "Doğum Tarihi", ["ikametgah_adresi"] = "İkametgâh Adresi",
        ["bolum"] = "Bölüm", ["teslim_yili"] = "Teslim Yılı", ["onceki_staj_gun"] = "Önceki Staj Günü",
        ["eposta"] = "E-posta", ["telefon"] = "Telefon", ["saglik_yardimi"] = "Sağlık Yardımı",
    };

    // Dictionary sırası ekleme sırasını korur; sütun sırası için ayrıca liste tutulur
    public static readonly string[] DatasetKeys =
    {
        "ad_soyad", "tc_kimlik_no", "uyruk", "ogrenci_no", "dogum_tarihi", "ikametgah_adresi",
        "bolum", "teslim_yili", "onceki_staj_gun", "eposta", "telefon", "saglik_yardimi"
    };
}
