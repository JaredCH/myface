using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;

namespace MyFace.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<MyFace.Core.Entities.Thread> Threads { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<PostImage> PostImages { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<OnionStatus> OnionStatuses { get; set; }
    public DbSet<PGPVerification> PGPVerifications { get; set; }
    public DbSet<UserContact> UserContacts { get; set; }
    public DbSet<UserNews> UserNews { get; set; }
    public DbSet<PageVisit> PageVisits { get; set; }
    public DbSet<UsernameChangeLog> UsernameChangeLogs { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<PrivateMessage> PrivateMessages { get; set; }
    public DbSet<UploadScanLog> UploadScanLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Thread configuration
        modelBuilder.Entity<MyFace.Core.Entities.Thread>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Threads)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Post configuration
        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ReportCount).HasDefaultValue(0);
            entity.Property(e => e.IsReportHidden).HasDefaultValue(false);
            entity.Property(e => e.WasModerated).HasDefaultValue(false);
            
            entity.HasOne(e => e.Thread)
                .WithMany(t => t.Posts)
                .HasForeignKey(e => e.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PostImage configuration
        modelBuilder.Entity<PostImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalPath).HasMaxLength(512).IsRequired();
            entity.Property(e => e.ThumbnailPath).HasMaxLength(512).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Post)
                .WithMany(p => p.Images)
                .HasForeignKey(e => e.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Vote configuration
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Value).IsRequired();
            
            // Ensure one vote per user per post (when userId is not null and postId is not null)
            entity.HasIndex(e => new { e.UserId, e.PostId }).HasFilter("\"UserId\" IS NOT NULL AND \"PostId\" IS NOT NULL").IsUnique();
            // Ensure one vote per session per post (when sessionId is not null and postId is not null)
            entity.HasIndex(e => new { e.SessionId, e.PostId }).HasFilter("\"SessionId\" IS NOT NULL AND \"PostId\" IS NOT NULL").IsUnique();
            // Ensure one vote per user per thread (when userId is not null and threadId is not null)
            entity.HasIndex(e => new { e.UserId, e.ThreadId }).HasFilter("\"UserId\" IS NOT NULL AND \"ThreadId\" IS NOT NULL").IsUnique();
            // Ensure one vote per session per thread (when sessionId is not null and threadId is not null)
            entity.HasIndex(e => new { e.SessionId, e.ThreadId }).HasFilter("\"SessionId\" IS NOT NULL AND \"ThreadId\" IS NOT NULL").IsUnique();
            
            entity.HasOne(e => e.Post)
                .WithMany(p => p.Votes)
                .HasForeignKey(e => e.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Thread)
                .WithMany(t => t.Votes)
                .HasForeignKey(e => e.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Votes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PGPVerification configuration
        modelBuilder.Entity<PGPVerification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Fingerprint).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ChallengeText).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Verified).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany(u => u.PGPVerifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OnionStatus configuration
        modelBuilder.Entity<OnionStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OnionUrl).IsUnique();
            entity.Property(e => e.OnionUrl).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ResponseTime);
            entity.Property(e => e.LastChecked).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ClickCount).HasDefaultValue(0);
        });

        // UserContact configuration
        modelBuilder.Entity<UserContact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ServiceName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccountId).HasMaxLength(100).IsRequired();
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Contacts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserNews configuration
        modelBuilder.Entity<UserNews>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.News)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Activity configuration
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActivityType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.CreatedAt); // Index for time-based queries
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // LoginAttempt configuration
        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LoginNameHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.IpAddressHash).HasMaxLength(64);
            entity.Property(e => e.AttemptedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.LoginNameHash, e.AttemptedAt }); // Composite index for lookups
        });

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Room).HasMaxLength(32).IsRequired();
            entity.Property(e => e.UsernameSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RoleSnapshot).HasMaxLength(16).HasDefaultValue("User");
            entity.Property(e => e.IsVerifiedSnapshot).HasDefaultValue(false);
            entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.Room, e.CreatedAt });
        });

        // PrivateMessage configuration
        modelBuilder.Entity<PrivateMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.SenderUsernameSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RecipientUsernameSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsDraft).HasDefaultValue(false);

            entity.HasIndex(e => new { e.RecipientId, e.CreatedAt });
            entity.HasIndex(e => new { e.SenderId, e.CreatedAt });

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Recipient)
                .WithMany()
                .HasForeignKey(e => e.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UploadScanLog configuration
        modelBuilder.Entity<UploadScanLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(64).IsRequired();
            entity.Property(e => e.SessionId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(e => e.ScanEngine).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ScanStatus).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.EventType, e.CreatedAt });
            entity.HasIndex(e => new { e.Source, e.CreatedAt });
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        });
    }
}
