using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

public sealed class ResumeMatcherDbContext(DbContextOptions<ResumeMatcherDbContext> options) : DbContext(options)
{
    public DbSet<Resume> Resumes
    {
        get
        {
            return Set<Resume>();
        }
    }

    public DbSet<Analysis> Analyses
    {
        get
        {
            return Set<Analysis>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Resume>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(100);
        });
        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ResumeId);
        });
    }
}

internal sealed class ResumeRepository(ResumeMatcherDbContext db) : IResumeRepository
{
    public async Task AddAsync(Resume resume, CancellationToken cancellationToken)
    {
        db.Resumes.Add(resume);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Resume?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Resumes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}

internal sealed class AnalysisRepository(ResumeMatcherDbContext db) : IAnalysisRepository
{
    public async Task AddAsync(Analysis analysis, CancellationToken cancellationToken)
    {
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Analysis?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Analyses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
