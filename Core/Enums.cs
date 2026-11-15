namespace StajEvrak.Core;

public enum Role
{
    Student,
    Admin
}

public static class RoleExtensions
{
    /// <summary>Veritabanında ve oturumda taşınan rol kodu.</summary>
    public static string Code(this Role role) => role == Role.Admin ? "ROLE_ADMIN" : "ROLE_STUDENT";

    public static Role FromCode(string code) => code switch
    {
        "ROLE_ADMIN" => Role.Admin,
        "ROLE_STUDENT" => Role.Student,
        _ => throw new ArgumentException($"Bilinmeyen rol: {code}")
    };
}

/// <summary>
/// Bölüm 5 — Belge statü akışı:
/// Yüklendi → İnceleniyor → Hatalı / Eksik → (yeniden yükleme) → Onaylandı → Tamamlandı
/// </summary>
public enum DocumentStatus
{
    TASLAK,        // Form doldurulmuş, henüz evrak yüklenmemiş
    YUKLENDI,
    INCELENIYOR,
    HATALI_EKSIK,
    ONAYLANDI,
    TAMAMLANDI
}

public static class DocumentStatusExtensions
{
    private static readonly Dictionary<DocumentStatus, string> Labels = new()
    {
        [DocumentStatus.TASLAK] = "Taslak",
        [DocumentStatus.YUKLENDI] = "Yüklendi",
        [DocumentStatus.INCELENIYOR] = "İnceleniyor",
        [DocumentStatus.HATALI_EKSIK] = "Hatalı / Eksik",
        [DocumentStatus.ONAYLANDI] = "Onaylandı",
        [DocumentStatus.TAMAMLANDI] = "Tamamlandı",
    };

    private static readonly Dictionary<DocumentStatus, string> CssClasses = new()
    {
        [DocumentStatus.TASLAK] = "draft",
        [DocumentStatus.YUKLENDI] = "uploaded",
        [DocumentStatus.INCELENIYOR] = "review",
        [DocumentStatus.HATALI_EKSIK] = "error",
        [DocumentStatus.ONAYLANDI] = "approved",
        [DocumentStatus.TAMAMLANDI] = "done",
    };

    // Sistem / öğrenci tarafından izin verilen geçişler (admin hepsini yapabilir)
    private static readonly Dictionary<DocumentStatus, HashSet<DocumentStatus>> Transitions = new()
    {
        [DocumentStatus.TASLAK] = new() { DocumentStatus.YUKLENDI },
        [DocumentStatus.YUKLENDI] = new() { DocumentStatus.INCELENIYOR },
        [DocumentStatus.INCELENIYOR] = new() { DocumentStatus.HATALI_EKSIK, DocumentStatus.ONAYLANDI, DocumentStatus.YUKLENDI },
        [DocumentStatus.HATALI_EKSIK] = new() { DocumentStatus.YUKLENDI, DocumentStatus.INCELENIYOR },
        [DocumentStatus.ONAYLANDI] = new() { DocumentStatus.TAMAMLANDI, DocumentStatus.HATALI_EKSIK, DocumentStatus.YUKLENDI, DocumentStatus.INCELENIYOR },
        [DocumentStatus.TAMAMLANDI] = new(),
    };

    public static string Label(this DocumentStatus s) => Labels[s];
    public static string Css(this DocumentStatus s) => CssClasses[s];
    public static bool CanTransitionTo(this DocumentStatus from, DocumentStatus to) => Transitions[from].Contains(to);

    /// <summary>Tamamlandı statüsündeki belgeyi öğrenci değiştiremez / silemez.</summary>
    public static bool IsLockedForStudent(this DocumentStatus s) => s == DocumentStatus.TAMAMLANDI;

    public static bool TryParseStatus(string value, out DocumentStatus status) =>
        Enum.TryParse(value, false, out status) && Enum.IsDefined(typeof(DocumentStatus), status);
}
