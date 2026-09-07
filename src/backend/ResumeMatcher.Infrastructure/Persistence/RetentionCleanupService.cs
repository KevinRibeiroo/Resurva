using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;

namespace ResumeMatcher.Infrastructure;

/// <summary>Privileged maintenance operation; never exposed as an HTTP endpoint.</summary>
public sealed class RetentionCleanupService(ResumeMatcherDbContext db, TimeProvider clock)
{
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-DataRetentionPolicy.Days);
        var expiredAnalyses = db.Analyses.Where(x => x.UpdatedAt <= cutoff ||
            db.Resumes.Any(r => r.Id == x.ResumeId && r.OwnerUserId == x.OwnerUserId && r.UpdatedAt <= cutoff));
        var expiredResumes = db.Resumes.Where(x => x.UpdatedAt <= cutoff);
        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var deleted = await expiredAnalyses.ExecuteDeleteAsync(cancellationToken);
            deleted += await expiredResumes.ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }

        db.Analyses.RemoveRange(await expiredAnalyses.ToListAsync(cancellationToken));
        db.Resumes.RemoveRange(await expiredResumes.ToListAsync(cancellationToken));
        return await db.SaveChangesAsync(cancellationToken);
    }
}
