using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;

namespace ResumeMatcher.Infrastructure;

/// <summary>Deletes application data only. Firebase identity deletion requires a separate account lifecycle.</summary>
internal sealed class AccountDataService(ResumeMatcherDbContext db, ICurrentUser currentUser) : IAccountDataService
{
    public async Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var optimizations = db.Optimizations.Where(x => x.OwnerUserId == owner);
        var analyses = db.Analyses.Where(x => x.OwnerUserId == owner);
        var resumes = db.Resumes.Where(x => x.OwnerUserId == owner);
        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await optimizations.ExecuteDeleteAsync(cancellationToken);
            await analyses.ExecuteDeleteAsync(cancellationToken);
            await resumes.ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        db.Optimizations.RemoveRange(await optimizations.ToListAsync(cancellationToken));
        db.Analyses.RemoveRange(await analyses.ToListAsync(cancellationToken));
        db.Resumes.RemoveRange(await resumes.ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }
}
