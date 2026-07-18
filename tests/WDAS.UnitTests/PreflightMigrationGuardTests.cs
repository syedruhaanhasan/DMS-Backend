using WDAS.Infrastructure.Migrations;

namespace WDAS.UnitTests;

public class PreflightMigrationGuardTests
{
    [Fact]
    public void EnsurePositiveId_RejectsNonPositiveValues()
    {
        Assert.Throws<InvalidOperationException>(() => PreflightMigrationGuard.EnsurePositiveId(0, "Id"));
        Assert.Throws<InvalidOperationException>(() => PreflightMigrationGuard.EnsurePositiveId(-1, "Id"));
    }

    [Fact]
    public void EnsureNoGuidPayload_RejectsGuidLikeStrings()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PreflightMigrationGuard.EnsureNoGuidPayload("00000000-0000-0000-0000-000000000001", "EntityId"));
    }

    [Fact]
    public void EnsureNoGuidPayload_AllowsIntegerStrings()
    {
        PreflightMigrationGuard.EnsureNoGuidPayload("42", "EntityId");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001", true)]
    [InlineData("42", false)]
    [InlineData(null, false)]
    public void LooksLikeGuid_DetectsGuidPayloads(string? value, bool expected)
    {
        Assert.Equal(expected, PreflightMigrationGuard.LooksLikeGuid(value));
    }
}
