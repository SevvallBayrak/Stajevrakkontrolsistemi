using StajEvrak.Core;
using StajEvrak.Data;

namespace StajEvrak.Web;

public sealed class StudentDashboardVm
{
    public List<Application> Applications { get; init; }
    public DatasetRecord Record { get; init; }
    public int Total { get; init; }
    public int Ready { get; init; }
    public int Problem { get; init; }
    public int Waiting { get; init; }
}

/// <summary>Ek-16 bilgi formu (öğrenci yeni/düzenle ve admin düzenle ekranlarında ortak).</summary>
public sealed class ApplicationFormVm
{
    public Dictionary<string, string> Data { get; init; } = new();
    public List<string> Days { get; init; } = new();
    public string Url { get; init; }
    public string UrlError { get; init; }
    public bool IsNew { get; init; }
    public bool AdminMode { get; init; }
    public Application Application { get; init; }

    public string Value(string key) => Data != null && Data.TryGetValue(key, out var v) ? v ?? "" : "";
}

public sealed class ApplicationDetailVm
{
    public Application Application { get; init; }
    public List<StatusHistoryItem> History { get; init; }
    public bool CanModify { get; init; }
    public DatasetRecord Record { get; init; }
}

public sealed class ReportVm
{
    public Document Document { get; init; }
}

public sealed class UploadVm
{
    public string Action { get; init; }
    public string Controller { get; init; }
    public int ApplicationId { get; init; }
    public bool HasDocument { get; init; }
}

public sealed class AdminDashboardVm
{
    public Dictionary<DocumentStatus, int> Counts { get; init; }
    public List<Application> Recent { get; init; }
    public int Total { get; init; }
    public int DatasetCount { get; init; }
    public int StudentCount { get; init; }
    public int FailedToday { get; init; }
    public int LockedCount { get; init; }
}

public sealed class ApplicationListVm
{
    public List<Application> Applications { get; init; }
    public string Status { get; init; }
    public string Query { get; init; }
    public Dictionary<DocumentStatus, int> Counts { get; init; }
}

public sealed class DatasetVm
{
    public List<DatasetRecord> Records { get; init; }
    public string Query { get; init; }
    public ImportResult Import { get; init; }
}

public sealed class UsersVm
{
    public List<UserListItem> Users { get; init; }
    public DateTime Now { get; init; }
}

public sealed class LoginLogsVm
{
    public List<LoginAttemptEntity> Logs { get; init; }
    public bool OnlyFailed { get; init; }
}
