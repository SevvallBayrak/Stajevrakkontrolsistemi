using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StajEvrak.Core;

namespace StajEvrak.Data;

/// <summary>Entity Framework Core veritabanı bağlamı (SQLite).</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserSessionEntity> Sessions => Set<UserSessionEntity>();
    public DbSet<LoginAttemptEntity> LoginAttempts => Set<LoginAttemptEntity>();
    public DbSet<DatasetRecord> DatasetRecords => Set<DatasetRecord>();
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<StatusHistoryEntity> StatusHistory => Set<StatusHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(30);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).IsRequired().HasMaxLength(20);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<UserSessionEntity>(e =>
        {
            e.ToTable("user_sessions");
            e.HasKey(x => x.Token);
            e.Property(x => x.Role).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LoginAttemptEntity>(e =>
        {
            e.ToTable("login_attempts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Identifier).IsRequired();
            e.HasIndex(x => x.Identifier);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<DatasetRecord>(e =>
        {
            e.ToTable("dataset_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.AdSoyad).IsRequired();
            e.Property(x => x.TcKimlikNo).IsRequired().HasMaxLength(11);
            e.Property(x => x.OgrenciNo).IsRequired();
            e.HasIndex(x => x.TcKimlikNo).IsUnique();
            e.HasIndex(x => x.OgrenciNo).IsUnique();
        });

        b.Entity<ApplicationEntity>(e =>
        {
            e.ToTable("applications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.FormData).IsRequired();
            e.HasOne(x => x.Student).WithMany(u => u.Applications).HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.StudentId);
        });

        b.Entity<DocumentEntity>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalName).IsRequired();
            e.Property(x => x.StoredName).IsRequired();
            e.Property(x => x.Extension).IsRequired();
            e.HasOne<ApplicationEntity>().WithMany(a => a.Documents).HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StatusHistoryEntity>(e =>
        {
            e.ToTable("status_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.NewStatus).IsRequired();
            e.HasOne<ApplicationEntity>().WithMany(a => a.History).HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Sütun adları: C# PascalCase → veritabanında snake_case (ör. FullName → full_name)
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

/// <summary>
/// Her işlem için yeni bir DbContext üretir. Repository'ler uygulama boyunca tek örnek (singleton)
/// olduğundan, DbContext'in (thread-safe olmadığı için) her sorguda yeniden oluşturulması gerekir.
/// </summary>
public sealed class AppDbContextFactory
{
    private readonly DbContextOptions<AppDbContext> _options;

    public AppDbContextFactory(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var connection = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    }

    public AppDbContext Create() => new(_options);
}

/// <summary>"dotnet ef migrations add ..." komutunun DbContext'i oluşturabilmesi için (tasarım zamanı).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        Directory.CreateDirectory("App_Data");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=App_Data/staj.db").Options;
        return new AppDbContext(options);
    }
}
