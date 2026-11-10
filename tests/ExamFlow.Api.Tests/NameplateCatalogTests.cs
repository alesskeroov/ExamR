using ExamFlow.Api.Features.Nameplates;

namespace ExamFlow.Api.Tests;

public class NameplateCatalogTests
{
    [Theory]
    [InlineData("np01")]
    [InlineData("np06")]
    [InlineData("np08")]
    [InlineData(null)]
    public void PublicEffectsAndClearingNeedNoGrant(string? id) =>
        Assert.True(NameplateCatalog.CanUse(id, false, 0));

    [Theory]
    [InlineData("np09", 1)]
    [InlineData("np11", 2)]
    [InlineData("np12", 4)]
    [InlineData("np14", 8)]
    public void EachRestrictedEffectNeedsItsOwnGrant(string id, int permission)
    {
        Assert.False(NameplateCatalog.CanUse(id, false, 0));
        Assert.False(NameplateCatalog.CanUse(id, false, 15 & ~permission));
        Assert.True(NameplateCatalog.CanUse(id, false, permission));
        Assert.True(NameplateCatalog.CanUse(id, true, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownEffectsAreRejectedEvenForAdmins(bool admin) =>
        Assert.False(NameplateCatalog.CanUse("np99", admin, 15));

    [Fact]
    public void RevokedOrDemotedUserCannotKeepAnUnlicensedActiveEffect()
    {
        Assert.Equal("np12", NameplateCatalog.State("np12", true, 0).Selected);
        Assert.Null(NameplateCatalog.State("np12", false, 0).Selected);
        Assert.Equal("np12", NameplateCatalog.State("np12", false, 4).Selected);
        Assert.Null(NameplateCatalog.State("np99", true, 15).Selected);
    }

    [Fact]
    public void AdminAccessDoesNotFabricatePermanentGrants()
    {
        var state = NameplateCatalog.State(null, true, 0);
        Assert.All(state.Options, option => Assert.True(option.Available));
        Assert.All(state.Options, option => Assert.False(option.Granted));
        Assert.Equal(4, state.Options.Count(option => option.Restricted));
    }
}
