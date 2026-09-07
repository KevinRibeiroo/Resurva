using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

internal sealed class ResumeRepository(ResumeMatcherDbContext db, ICurrentUser currentUser, TimeProvider clock) : IResumeRepository
{
    public async Task AddAsync(ResumeEntity resume, CancellationToken cancellationToken)
    {
        if (resume.OwnerUserId != currentUser.UserId)
            throw new InvalidOperationException("The resume owner must match the current user.");
        resume.UpdatedAt = clock.GetUtcNow();
        db.Resumes.Add(resume);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<ResumeEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var cutoff = clock.GetUtcNow().AddDays(-DataRetentionPolicy.Days);
        return db.Resumes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == owner && x.UpdatedAt > cutoff, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var resume = await db.Resumes.SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == owner, cancellationToken);
        if (resume is null)
            return false;

        var analyses = await db.Analyses.Where(x => x.ResumeId == id && x.OwnerUserId == owner).ToListAsync(cancellationToken);
        db.Analyses.RemoveRange(analyses);
        db.Resumes.Remove(resume);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
