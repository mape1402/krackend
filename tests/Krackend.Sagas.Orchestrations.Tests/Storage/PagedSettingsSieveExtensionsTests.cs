namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using System.Reflection;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Sieve.Models;

public sealed class PagedSettingsSieveExtensionsTests
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void ToSieveModel_WhenSettingsAreNull_UsesSafeDefaults()
    {
        var model = ToSieveModel(null);

        Assert.Equal(1, model.Page);
        Assert.Equal(25, model.PageSize);
        Assert.Null(model.Sorts);
        Assert.Null(model.Filters);
    }

    [Fact]
    public void ToSieveModel_NormalizesPagingSortsFiltersAndEscapesValues()
    {
        var settings = new PagedSettings(
            0,
            -5,
            [
                new QueryFilter("name", "eq", "Sale \"A\"\\B"),
                new QueryFilter("key", "neq", "abc"),
                new QueryFilter("description", "contains", "created"),
                new QueryFilter("domain", "startswith", "sales"),
                new QueryFilter("ownerTeam", "endswith", "team"),
                new QueryFilter("order", "gt", "1"),
                new QueryFilter("attempts", "gte", "2"),
                new QueryFilter("priority", "lt", "9"),
                new QueryFilter("version", "lte", "10"),
                new QueryFilter("fallback", "unknown", null!),
                null!,
                new QueryFilter(" ", "eq", "ignored")
            ],
            [
                new QuerySort("key", false),
                new QuerySort("name", true),
                null!,
                new QuerySort(" ", false)
            ]);

        var model = ToSieveModel(settings);

        Assert.Equal(1, model.Page);
        Assert.Equal(1, model.PageSize);
        Assert.Equal("key,-name", model.Sorts);
        Assert.Contains("name==\"Sale \\\"A\\\"\\\\B\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("key!=\"abc\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("description@=\"created\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("domain_=\"sales\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("ownerTeam_-= \"team\"".Replace(" ", string.Empty, StringComparison.Ordinal), model.Filters, StringComparison.Ordinal);
        Assert.Contains("order>\"1\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("attempts>=\"2\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("priority<\"9\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("version<=\"10\"", model.Filters, StringComparison.Ordinal);
        Assert.Contains("fallback==\"\"", model.Filters, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSieveModel_WhenFiltersAndSortsAreEmpty_ReturnsNullExpressions()
    {
        var model = ToSieveModel(new PagedSettings(2, 50, [], []));

        Assert.Equal(2, model.Page);
        Assert.Equal(50, model.PageSize);
        Assert.Null(model.Sorts);
        Assert.Null(model.Filters);
    }

    private static SieveModel ToSieveModel(PagedSettings? settings)
    {
        var type = typeof(ControlPlaneDbContext).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure.PagedSettingsSieveExtensions",
            throwOnError: true)!;
        var method = type.GetMethod("ToSieveModel", StaticFlags)!;
        return (SieveModel)method.Invoke(null, [settings])!;
    }
}
