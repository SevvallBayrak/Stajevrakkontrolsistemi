namespace StajEvrak.Data;

/*
 * Entity Framework Core varlık (entity) sınıfları — veritabanı tablolarının C# karşılığı.
 * İş kuralları bu sınıflarda değil, Core klasöründeki alan (domain) sınıflarındadır
 * (User / Student / Admin, Application ...). Repository'ler bu iki katman arasında dönüşüm yapar.
 * Sütun adları AppDbContext içinde otomatik olarak snake_case yapılır (FullName → full_name).
 */

public class UserEntity
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string Role { get; set; }              // ROLE_STUDENT / ROLE_ADMIN
    public string FullName { get; set; }
    public string StudentNo { get; set; }
    public string TcKimlikNo { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ApplicationEntity> Applications { get; set; } = new();
}

public class UserSessionEntity
{
    public string Token { get; set; }             // birincil anahtar
    public int UserId { get; set; }
    public UserEntity User { get; set; }
    public string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}

public class LoginAttemptEntity
{
    public int Id { get; set; }
    public string Identifier { get; set; }
    public int? UserId { get; set; }
    public string IpAddress { get; set; }
    public bool Success { get; set; }
    public string Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApplicationEntity
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public UserEntity Student { get; set; }
    public string Status { get; set; }
    public string FormData { get; set; }          // Ek-16 bilgileri (JSON)
    public string StajGunleri { get; set; }       // seçilen günler (JSON)
    public string GoogleFormUrl { get; set; }
    public string AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<DocumentEntity> Documents { get; set; } = new();
    public List<StatusHistoryEntity> History { get; set; } = new();
}

public class DocumentEntity
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string OriginalName { get; set; }
    public string StoredName { get; set; }
    public string Extension { get; set; }
    public long SizeBytes { get; set; }
    public string ExtractedData { get; set; }     // belgeden okunan alanlar (JSON)
    public string Report { get; set; }            // karşılaştırma raporu (JSON)
    public bool IsCurrent { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class StatusHistoryEntity
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string OldStatus { get; set; }
    public string NewStatus { get; set; }
    public int? ChangedBy { get; set; }
    public UserEntity ChangedByUser { get; set; }
    public string Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
