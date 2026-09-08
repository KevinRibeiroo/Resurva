using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

internal sealed class ResumeOptimizationRepository(
    ResumeMatcherDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock) : IResumeOptimizationRepository
{
    public async Task AddAsync(ResumeOptimizationEntity entity, CancellationToken cancellationToken)
    {
        if (entity.OwnerUserId != currentUser.UserId)
            throw new InvalidOperationException("The optimization owner must match the current user.");

        entity.UpdatedAt = clock.GetUtcNow();
        db.Optimizations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ResumeOptimizationEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var cutoff = clock.GetUtcNow().AddDays(-DataRetentionPolicy.Days);

        return db.Optimizations.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id &&
                 x.OwnerUserId == owner &&
                 x.UpdatedAt > cutoff &&
                 db.Resumes.Any(r => r.Id == x.ResumeId && r.OwnerUserId == owner && r.UpdatedAt > cutoff) &&
                 db.Analyses.Any(a => a.Id == x.AnalysisId && a.OwnerUserId == owner && a.UpdatedAt > cutoff),
            cancellationToken);
    }

    public async Task UpdateAsync(ResumeOptimizationEntity entity, CancellationToken cancellationToken)
    {
        if (entity.OwnerUserId != currentUser.UserId)
            throw new InvalidOperationException("The optimization owner must match the current user.");

        entity.UpdatedAt = clock.GetUtcNow();
        db.Optimizations.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
