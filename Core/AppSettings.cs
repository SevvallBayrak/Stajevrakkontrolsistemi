namespace StajEvrak.Core;

/// <summary>appsettings.json → "App" bölümü.</summary>
public class AppSettings
{
    public string DatabasePath { get; set; } = "App_Data/staj.db";
    public string UploadFolder { get; set; } = "App_Data/uploads";
    public string DatasetFolder { get; set; } = "App_Data/datasets";
    public int MaxDocumentSizeMb { get; set; } = 10;
    public int MaxFailedLogins { get; set; } = 5;
    public int LockMinutes { get; set; } = 15;
    public int SessionHours { get; set; } = 8;
    public string DefaultAdminUsername { get; set; } = "admin";
    public string DefaultAdminEmail { get; set; } = "admin@kocaeli.edu.tr";
    public string DefaultAdminPassword { get; set; } = "Admin123!";

    // Bölüm 4.4 — kabul edilen evrak formatları
    public static readonly string[] AllowedDocumentExtensions = { ".doc", ".docx", ".pdf", ".jpg", ".jpeg", ".png" };
    public static readonly string[] AllowedDatasetExtensions = { ".csv", ".xlsx" };

    public long MaxDocumentBytes => MaxDocumentSizeMb * 1024L * 1024L;

    /// <summary>Göreli yolları proje klasörüne göre mutlak yola çevirir.</summary>
    public void Resolve(string contentRoot)
    {
        DatabasePath = Path.GetFullPath(Path.Combine(contentRoot, DatabasePath));
        UploadFolder = Path.GetFullPath(Path.Combine(contentRoot, UploadFolder));
        DatasetFolder = Path.GetFullPath(Path.Combine(contentRoot, DatasetFolder));
    }
}
