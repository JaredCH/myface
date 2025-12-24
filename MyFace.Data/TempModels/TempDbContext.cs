using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace MyFace.Data.TempModels;

public partial class TempDbContext : DbContext
{
    public TempDbContext()
    {
    }

    public TempDbContext(DbContextOptions<TempDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<OnionStatus> OnionStatuses { get; set; }

    public virtual DbSet<PageVisit> PageVisits { get; set; }

    public virtual DbSet<Pgpverification> Pgpverifications { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<Thread> Threads { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserContact> UserContacts { get; set; }

    public virtual DbSet<UserNews> UserNews { get; set; }

    public virtual DbSet<UsernameChangeLog> UsernameChangeLogs { get; set; }

    public virtual DbSet<Vote> Votes { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OnionStatus>(entity =>
        {
            entity.HasIndex(e => e.OnionUrl, "IX_OnionStatuses_OnionUrl").IsUnique();

            entity.Property(e => e.Description).HasDefaultValueSql("''::text");
            entity.Property(e => e.LastChecked).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Name).HasDefaultValueSql("''::text");
            entity.Property(e => e.OnionUrl).HasMaxLength(100);
            entity.Property(e => e.ReachableAttempts).HasDefaultValue(0);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.TotalAttempts).HasDefaultValue(0);
        });

        modelBuilder.Entity<Pgpverification>(entity =>
        {
            entity.ToTable("PGPVerifications");

            entity.HasIndex(e => e.UserId, "IX_PGPVerifications_UserId");

            entity.Property(e => e.ChallengeText).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Fingerprint).HasMaxLength(64);
            entity.Property(e => e.Verified).HasDefaultValue(false);

            entity.HasOne(d => d.User).WithMany(p => p.Pgpverifications).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasIndex(e => e.ThreadId, "IX_Posts_ThreadId");

            entity.HasIndex(e => e.UserId, "IX_Posts_UserId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsSticky).HasDefaultValue(false);

            entity.HasOne(d => d.Thread).WithMany(p => p.Posts).HasForeignKey(d => d.ThreadId);

            entity.HasOne(d => d.User).WithMany(p => p.Posts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Thread>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_Threads_UserId");

            entity.Property(e => e.Category).HasDefaultValueSql("''::text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.User).WithMany(p => p.Threads)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username, "IX_Users_Username").IsUnique();

            entity.Property(e => e.AboutMe).HasDefaultValueSql("''::text");
            entity.Property(e => e.AccentColor).HasDefaultValueSql("''::text");
            entity.Property(e => e.BackgroundColor).HasDefaultValueSql("''::text");
            entity.Property(e => e.BorderColor).HasDefaultValueSql("''::text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CustomCss)
                .HasDefaultValueSql("''::text")
                .HasColumnName("CustomCSS");
            entity.Property(e => e.FontColor).HasDefaultValueSql("''::text");
            entity.Property(e => e.FontFamily).HasDefaultValueSql("''::text");
            entity.Property(e => e.FontSize).HasDefaultValue(0);
            entity.Property(e => e.MustChangeUsername).HasDefaultValue(false);
            entity.Property(e => e.ProfileLayout).HasDefaultValueSql("''::text");
            entity.Property(e => e.Role).HasDefaultValueSql("'User'::text");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<UserContact>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_UserContacts_UserId");

            entity.Property(e => e.AccountId).HasMaxLength(100);
            entity.Property(e => e.ServiceName).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.UserContacts).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<UserNews>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_UserNews_UserId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.UserNews).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<UsernameChangeLog>(entity =>
        {
            entity.HasIndex(e => e.ChangedByUserId, "IX_UsernameChangeLogs_ChangedByUserId");

            entity.HasIndex(e => e.UserId, "IX_UsernameChangeLogs_UserId");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.UsernameChangeLogChangedByUsers).HasForeignKey(d => d.ChangedByUserId);

            entity.HasOne(d => d.User).WithMany(p => p.UsernameChangeLogUsers).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasIndex(e => e.PostId, "IX_Votes_PostId");

            entity.HasIndex(e => new { e.SessionId, e.PostId }, "IX_Votes_SessionId_PostId")
                .IsUnique()
                .HasFilter("(\"SessionId\" IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.PostId }, "IX_Votes_UserId_PostId")
                .IsUnique()
                .HasFilter("(\"UserId\" IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Post).WithMany(p => p.Votes).HasForeignKey(d => d.PostId);

            entity.HasOne(d => d.User).WithMany(p => p.Votes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
