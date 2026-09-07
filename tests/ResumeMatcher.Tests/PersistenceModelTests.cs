using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void AnalysisHasRequiredResumeForeignKeyAndUniqueInputHash()
    {
        var options = new DbContextOptionsBuilder<ResumeMatcherDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=postgres")
            .Options;
        using var db = new ResumeMatcherDbContext(options);
        var entity = db.Model.FindEntityType(typeof(AnalysisEntity));

        Assert.NotNull(entity);
        var foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(nameof(AnalysisEntity.ResumeId), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(typeof(ResumeEntity), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);

        var hashIndex = Assert.Single(entity.GetIndexes(), index =>
            index.Properties.Single().Name == nameof(AnalysisEntity.AnalysisInputHash));
        Assert.True(hashIndex.IsUnique);
    }
}
