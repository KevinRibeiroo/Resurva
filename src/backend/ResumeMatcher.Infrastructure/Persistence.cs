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
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(100);
        });
        modelBuilder.Entity<AnalysisEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ResumeId);
        });
    }
}

internal sealed class ResumeRepository(ResumeMatcherDbContext db) : IResumeRepository
{
    public async Task AddAsync(ResumeEntity resume, CancellationToken cancellationToken)
    {
        db.Resumes.Add(resume);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ResumeEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Resumes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}

internal sealed class AnalysisRepository(ResumeMatcherDbContext db) : IAnalysisRepository
{
    public async Task AddAsync(AnalysisEntity analysis, CancellationToken cancellationToken)
    {
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<AnalysisEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Analyses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
