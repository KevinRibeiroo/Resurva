using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

internal sealed class AnalysisRepository(ResumeMatcherDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAnalysisRepository
{
    public async Task<bool> TryAddAsync(AnalysisEntity analysis, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        if (analysis.OwnerUserId != owner)
            throw new InvalidOperationException("The analysis owner must match the current user.");
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-DataRetentionPolicy.Days);
        var resume = await db.Resumes.SingleOrDefaultAsync(x => x.Id == analysis.ResumeId && x.OwnerUserId == owner && x.UpdatedAt > cutoff, cancellationToken);
        if (resume is null)
            throw new ResourceNotFoundException("Resume not found.");
        // Remove an expired cache entry before replacing it; reads never renew retention.
        var expired = await db.Analyses.Where(x => x.OwnerUserId == owner && x.AnalysisInputHash == analysis.AnalysisInputHash && x.UpdatedAt <= cutoff).ToListAsync(cancellationToken);
        db.Analyses.RemoveRange(expired);
        analysis.UpdatedAt = now;
        resume.UpdatedAt = now;
        db.Analyses.Add(analysis);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException) when (analysis.AnalysisInputHash is not null)
        {
            db.Entry(analysis).State = EntityState.Detached;
            db.Entry(resume).State = EntityState.Unchanged;
            foreach (var item in expired)
                db.Entry(item).State = EntityState.Detached;
            if (await db.Analyses.AsNoTracking().AnyAsync(
                    x => x.OwnerUserId == owner && x.AnalysisInputHash == analysis.AnalysisInputHash && x.UpdatedAt > cutoff,
                    cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    public Task<AnalysisEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var cutoff = clock.GetUtcNow().AddDays(-DataRetentionPolicy.Days);
        return db.Analyses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == owner && x.UpdatedAt > cutoff &&
            db.Resumes.Any(r => r.Id == x.ResumeId && r.OwnerUserId == owner && r.UpdatedAt > cutoff), cancellationToken);
    }

    public Task<AnalysisEntity?> GetByInputHashAsync(string analysisInputHash, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var cutoff = clock.GetUtcNow().AddDays(-DataRetentionPolicy.Days);
        return db.Analyses.AsNoTracking().SingleOrDefaultAsync(
            x => x.OwnerUserId == owner && x.AnalysisInputHash == analysisInputHash && x.UpdatedAt > cutoff &&
                db.Resumes.Any(r => r.Id == x.ResumeId && r.OwnerUserId == owner && r.UpdatedAt > cutoff),
            cancellationToken);
    }
}
