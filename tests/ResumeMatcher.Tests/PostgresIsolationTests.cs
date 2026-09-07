using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class PostgresIsolationTests
{
    [PostgresFact]
    public async Task MigrationsOwnershipCacheConstraintsCascadeAndRetentionWorkOnPostgres()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RESUMEMATCHER_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost", "::1" });
        connection.Pooling = false;
        var databaseName = $"resume_matcher_tests_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();
        connection.Database = databaseName;
        var options = new DbContextOptionsBuilder<ResumeMatcherDbContext>().UseNpgsql(connection.ConnectionString).Options;
        try
        {
            var clock = new TestTimeProvider();
            await using (var db = new ResumeMatcherDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync("20260904202949_AddAnalysisResumeForeignKey");
                var oldId = Guid.NewGuid();
                var created = clock.GetUtcNow().AddDays(-10);
                var lastAnalysisAt = clock.GetUtcNow().AddDays(-5);
                var oldAnalysisId = Guid.NewGuid();
                var resultJson = "{}";
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Resumes\" (\"Id\", \"FileName\", \"ContentType\", \"ExtractedText\", \"CreatedAt\") VALUES ({oldId}, 'legacy.pdf', 'application/pdf', 'Synthetic legacy text', {created})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Analyses\" (\"Id\", \"ResumeId\", \"JobDescription\", \"ResultJson\", \"OverallScore\", \"SkillsScore\", \"ExperienceScore\", \"SeniorityScore\", \"RequirementsScore\", \"EducationScore\", \"CreatedAt\") VALUES ({oldAnalysisId}, {oldId}, 'Synthetic job', {resultJson}, 0, 0, 0, 0, 0, 0, {lastAnalysisAt})");
                await db.Database.MigrateAsync();
                Assert.False(db.Database.HasPendingModelChanges());
                var legacy = await db.Resumes.SingleAsync();
                Assert.Equal("", legacy.OwnerUserId);
                // PostgreSQL timestamps have microsecond precision.
                Assert.True((legacy.UpdatedAt - lastAnalysisAt).Duration() < TimeSpan.FromMilliseconds(1));
                Assert.Equal("", (await db.Analyses.SingleAsync()).OwnerUserId);
                // Explicit operator assignment preserves age, unlike a login-triggered backfill.
                await using var transaction = await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS \"FK_Analyses_Resumes_OwnerUserId_ResumeId\" DEFERRED");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Resumes\" SET \"OwnerUserId\" = 'legacy-owner' WHERE \"Id\" = {oldId} AND \"OwnerUserId\" = ''");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Analyses\" SET \"OwnerUserId\" = 'legacy-owner' WHERE \"ResumeId\" = {oldId} AND \"OwnerUserId\" = ''");
                await transaction.CommitAsync();
            }

            await using var providerA = BuildProvider(connection.ConnectionString, "user-a", clock);
            await using var providerB = BuildProvider(connection.ConnectionString, "user-b", clock);
            await using var scopeA = providerA.CreateAsyncScope();
            await using var scopeB = providerB.CreateAsyncScope();
            var resumesA = scopeA.ServiceProvider.GetRequiredService<IResumeRepository>();
            var resumesB = scopeB.ServiceProvider.GetRequiredService<IResumeRepository>();
            var analysesA = scopeA.ServiceProvider.GetRequiredService<IAnalysisRepository>();
            var analysesB = scopeB.ServiceProvider.GetRequiredService<IAnalysisRepository>();
            var a = Resume("user-a");
            var b = Resume("user-b");
            await resumesA.AddAsync(a, CancellationToken.None);
            await resumesB.AddAsync(b, CancellationToken.None);
            var aa = Analysis(a, "same-hash");
            var ab = Analysis(b, "same-hash");
            Assert.True(await analysesA.TryAddAsync(aa, CancellationToken.None));
            Assert.True(await analysesB.TryAddAsync(ab, CancellationToken.None));
            Assert.False(await analysesA.TryAddAsync(Analysis(a, "same-hash"), CancellationToken.None));
            Assert.Equal(aa.Id, (await analysesA.GetByInputHashAsync("same-hash", CancellationToken.None))!.Id);
            Assert.Equal(ab.Id, (await analysesB.GetByInputHashAsync("same-hash", CancellationToken.None))!.Id);
            Assert.Null(await resumesB.GetAsync(a.Id, CancellationToken.None));
            Assert.Null(await analysesB.GetAsync(aa.Id, CancellationToken.None));
            Assert.False(await resumesB.DeleteAsync(a.Id, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => resumesA.AddAsync(Resume("user-b"), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => analysesA.TryAddAsync(ab, CancellationToken.None));

            // Check the database boundary even if application checks are bypassed.
            await using (var direct = new ResumeMatcherDbContext(options))
            {
                direct.Analyses.Add(Analysis(a, "invalid-owner", "user-b"));
                await Assert.ThrowsAsync<DbUpdateException>(() => direct.SaveChangesAsync());
            }

            // Database cascade, independently of the repository's explicit deletion.
            await using (var direct = new ResumeMatcherDbContext(options))
            {
                direct.Optimizations.Add(new ResumeOptimizationEntity
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = a.OwnerUserId,
                    ResumeId = a.Id,
                    AnalysisId = aa.Id,
                    OriginalText = a.ExtractedText,
                    Version = 1,
                    Status = "Pending",
                    SuggestionsJson = "[]",
                    CreatedAt = clock.GetUtcNow(),
                    UpdatedAt = clock.GetUtcNow()
                });
                await direct.SaveChangesAsync();

                await direct.Resumes.Where(x => x.Id == a.Id).ExecuteDeleteAsync();
                Assert.False(await direct.Analyses.AnyAsync(x => x.Id == aa.Id));
                Assert.False(await direct.Optimizations.AnyAsync(x => x.ResumeId == a.Id));
                Assert.True(await direct.Analyses.AnyAsync(x => x.Id == ab.Id));
            }

            var anotherA = Resume("user-a");
            await resumesA.AddAsync(anotherA, CancellationToken.None);
            Assert.True(await analysesA.TryAddAsync(Analysis(anotherA, "account-clear"), CancellationToken.None));
            await scopeA.ServiceProvider.GetRequiredService<IAccountDataService>().DeleteAllAsync(CancellationToken.None);
            await scopeA.ServiceProvider.GetRequiredService<IAccountDataService>().DeleteAllAsync(CancellationToken.None);
            Assert.Null(await resumesA.GetAsync(anotherA.Id, CancellationToken.None));
            Assert.NotNull(await resumesB.GetAsync(b.Id, CancellationToken.None));

            clock.Advance(TimeSpan.FromDays(29));
            Assert.True(await analysesB.TryAddAsync(Analysis(b, "new-job"), CancellationToken.None));
            clock.Advance(TimeSpan.FromDays(1));
            Assert.Null(await analysesB.GetByInputHashAsync("same-hash", CancellationToken.None));
            Assert.True(await analysesB.TryAddAsync(Analysis(b, "same-hash"), CancellationToken.None));
            Assert.NotEqual(ab.Id, (await analysesB.GetByInputHashAsync("same-hash", CancellationToken.None))!.Id);

            clock.Advance(TimeSpan.FromDays(31));
            Assert.Null(await resumesB.GetAsync(b.Id, CancellationToken.None));
            Assert.Null(await analysesB.GetByInputHashAsync("same-hash", CancellationToken.None));
            Assert.True(await scopeB.ServiceProvider.GetRequiredService<RetentionCleanupService>().PurgeExpiredAsync(CancellationToken.None) >= 2);
            await using var verify = new ResumeMatcherDbContext(options);
            Assert.Empty(await verify.Resumes.ToListAsync());
            Assert.Empty(await verify.Analyses.ToListAsync());
            Assert.Empty(await verify.Optimizations.ToListAsync());
        }
        finally
        {
            // Only this randomly named database, created by this test, may be removed.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\"", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static ServiceProvider BuildProvider(string connection, string owner, TimeProvider clock)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ResumeMatcher"] = connection }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddSingleton(clock);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(owner));
        return services.BuildServiceProvider();
    }

    private static ResumeEntity Resume(string owner) => new() { OwnerUserId = owner, FileName = "synthetic.pdf", ContentType = "application/pdf", ExtractedText = "Synthetic text" };
    private static AnalysisEntity Analysis(ResumeEntity resume, string hash, string? owner = null) => new()
    {
        OwnerUserId = owner ?? resume.OwnerUserId, ResumeId = resume.Id, AnalysisInputHash = hash, JobDescription = "Synthetic job", ResultJson = "{}"
    };
}
