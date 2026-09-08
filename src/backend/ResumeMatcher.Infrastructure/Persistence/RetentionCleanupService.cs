using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;

namespace ResumeMatcher.Infrastructure;

/// <summary>Privileged maintenance operation; never exposed as an HTTP endpoint.</summary>
public sealed class RetentionCleanupService(ResumeMatcherDbContext db, TimeProvider clock)
{
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-DataRetentionPolicy.Days);
        var expiredOptimizations = db.Optimizations.Where(x => x.UpdatedAt <= cutoff ||
            db.Resumes.Any(r => r.Id == x.ResumeId && r.OwnerUserId == x.OwnerUserId && r.UpdatedAt <= cutoff) ||
            db.Analyses.Any(a => a.Id == x.AnalysisId && a.OwnerUserId == x.OwnerUserId && a.UpdatedAt <= cutoff));
        var expiredAnalyses = db.Analyses.Where(x => x.UpdatedAt <= cutoff ||
            db.Resumes.Any(r => r.Id == x.ResumeId && r.OwnerUserId == x.OwnerUserId && r.UpdatedAt <= cutoff));
        var expiredResumes = db.Resumes.Where(x => x.UpdatedAt <= cutoff);
        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var deleted = await expiredOptimizations.ExecuteDeleteAsync(cancellationToken);
            deleted += await expiredAnalyses.ExecuteDeleteAsync(cancellationToken);
            deleted += await expiredResumes.ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }

        db.Optimizations.RemoveRange(await expiredOptimizations.ToListAsync(cancellationToken));
        db.Analyses.RemoveRange(await expiredAnalyses.ToListAsync(cancellationToken));
        db.Resumes.RemoveRange(await expiredResumes.ToListAsync(cancellationToken));
        return await db.SaveChangesAsync(cancellationToken);
    }
}
