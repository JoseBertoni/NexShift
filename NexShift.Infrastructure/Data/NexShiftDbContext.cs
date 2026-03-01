using Microsoft.EntityFrameworkCore;
using NexShift.Core.Entities;

namespace NexShift.Infrastructure.Data;

public class NexShiftDbContext : DbContext
{
    public NexShiftDbContext(DbContextOptions<NexShiftDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<MigrationJob> MigrationJobs => Set<MigrationJob>();
    public DbSet<MigrationIssue> MigrationIssues => Set<MigrationIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GitHubId).IsRequired();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.GitHubId).IsUnique();
        });

        // Repository
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Repositories)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // MigrationJob
        modelBuilder.Entity<MigrationJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TargetFramework).IsRequired().HasMaxLength(20);
            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.MigrationJobs)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // MigrationIssue
        modelBuilder.Entity<MigrationIssue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).IsRequired();
            entity.HasOne(e => e.MigrationJob)
                  .WithMany(j => j.Issues)
                  .HasForeignKey(e => e.MigrationJobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}