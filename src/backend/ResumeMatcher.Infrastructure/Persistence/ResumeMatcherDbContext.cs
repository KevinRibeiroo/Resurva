using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

public sealed class ResumeMatcherDbContext(DbContextOptions<ResumeMatcherDbContext> options) : DbContext(options)
{
    public DbSet<ResumeEntity> Resumes
    {
        get
        {
            return Set<ResumeEntity>();
        }
    }

    public DbSet<AnalysisEntity> Analyses
    {
        get
        {
            return Set<AnalysisEntity>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResumeEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OwnerUserId).HasMaxLength(128).IsRequired();
            entity.HasAlternateKey(x => new { x.OwnerUserId, x.Id });
            entity.HasIndex(x => x.UpdatedAt);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(100);
        });
        modelBuilder.Entity<AnalysisEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OwnerUserId).HasMaxLength(128).IsRequired();
            entity.HasOne<ResumeEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.OwnerUserId, x.ResumeId })
                .HasPrincipalKey(x => new { x.OwnerUserId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.AnalysisInputHash).HasMaxLength(64);
            entity.HasIndex(x => new { x.OwnerUserId, x.AnalysisInputHash }).IsUnique();
            entity.HasIndex(x => x.UpdatedAt);
        });
    }
}
