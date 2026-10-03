using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Security;

public sealed class EntityFrameworkKrackendSubjectRepositoryTests
{
    [Fact]
    public async Task SubjectRepositoryCreatesUpdatesQueriesPagesAndTogglesSubjects()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KrackendSecurityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendSubjectRepository>();

        var created = new KrackendSubject
        {
            Provider = " azure-ad ",
            SubjectId = " user-b ",
            DisplayName = "Beta User",
            Email = "beta@example.test",
            IsEnabled = true
        };
        await repository.Upsert(created);
        await repository.Upsert(new KrackendSubject
        {
            Provider = "azure-ad",
            SubjectId = "user-a",
            DisplayName = "Alpha User",
            Email = "alpha@example.test",
            IsEnabled = true
        });

        var stored = await repository.GetByExternalSubject("azure-ad", "user-b");
        Assert.False(string.IsNullOrWhiteSpace(stored.Id));
        Assert.Equal("azure-ad", stored.Provider);
        Assert.Equal("user-b", stored.SubjectId);

        await repository.Upsert(new KrackendSubject
        {
            Id = stored.Id,
            Provider = "azure-ad",
            SubjectId = "user-b",
            DisplayName = null!,
            Email = null!,
            IsEnabled = false
        });
        await repository.SetEnabled(stored.Id, true);

        var updated = await repository.GetById(stored.Id);
        Assert.Equal(string.Empty, updated.DisplayName);
        Assert.Equal(string.Empty, updated.Email);
        Assert.True(updated.IsEnabled);
        Assert.NotEqual(default, updated.CreatedAtUtc);
        Assert.NotEqual(default, updated.UpdatedAtUtc);

        var firstPage = await repository.GetAll(0, 0);
        var searchPage = await repository.GetAll(1, 25, "alpha");

        Assert.Equal(1, firstPage.PageNumber);
        Assert.Equal(25, firstPage.PageSize);
        Assert.Equal(2, firstPage.TotalRows);
        Assert.Equal([string.Empty, "Alpha User"], firstPage.Rows.Select(x => x.DisplayName).ToArray());
        Assert.Single(searchPage.Rows);
        Assert.Equal("alpha@example.test", searchPage.Rows.Single().Email);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.SetEnabled("missing", false));
    }

    [Fact]
    public async Task SubjectRepositoryNormalizesNullFieldsAndNullLookupArguments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KrackendSecurityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendSubjectRepository>();

        await repository.Upsert(new KrackendSubject
        {
            Provider = null!,
            SubjectId = null!,
            DisplayName = null!,
            Email = null!,
            IsEnabled = true
        });

        var stored = await repository.GetByExternalSubject(null!, null!);
        var page = await repository.GetAll(-1, -1, " ");

        Assert.NotNull(stored);
        Assert.Equal(string.Empty, stored.Provider);
        Assert.Equal(string.Empty, stored.SubjectId);
        Assert.Single(page.Rows);
    }

    private static ServiceProvider CreateProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddKrackendSecurityStorageEntityFramework(options => options.UseSqlite(connection));
        return services.BuildServiceProvider();
    }
}
