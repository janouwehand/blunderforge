using System.Net;
using System.Net.Http.Json;
using BlunderForge.Application.Engine;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace BlunderForge.WebTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task HealthReturnsOk()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>(CancellationToken.None);
        Assert.Equal("Healthy", body?.Status);
    }

    [Fact]
    public async Task AiProviderStatusDoesNotExposeSecretValue()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/settings/ai-provider/status", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("secretAvailable", text, StringComparison.Ordinal);
        Assert.Contains("BLUNDERFORGE_DEEPSEEK_API_KEY", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example_fake", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadyUsesStockfishHealthService()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("blunderforge-ready-");
        using var readyFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BlunderForge:DataDirectory", dataDirectory.FullName);
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(dataDirectory.FullName, "blunderforge.db")}");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IEngineHealthService>(new FakeEngineHealthService(
                    new EngineHealthResult(true, "Ready", "Fake Stockfish is ready.", "Fakefish 1")));
            });
        });
        try
        {
            using var client = readyFactory.CreateClient();
            var response = await client.GetAsync("/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
            Assert.Contains("Fake Stockfish is ready.", text, StringComparison.Ordinal);
        }
        finally { DeleteDirectoryWithRetry(dataDirectory.FullName); }
    }

    [Fact]
    public async Task ReadyReportsDatabaseReadyAfterStartupMigration()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("blunderforge-web-db-");
        Environment.SetEnvironmentVariable("BlunderForge__DataDirectory", dataDirectory.FullName);
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"Data Source={Path.Combine(dataDirectory.FullName, "blunderforge.db")}");
        try
        {
            using var readyFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IEngineHealthService>(new FakeEngineHealthService(
                        new EngineHealthResult(true, "Ready", "Fake Stockfish is ready.", "Fakefish 1")));
                });
            });
            using var client = readyFactory.CreateClient();

            var response = await client.GetAsync("/ready", CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
            Assert.Contains("Database migrations applied successfully.", text, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(dataDirectory.FullName, "blunderforge.db")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BlunderForge__DataDirectory", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
            DeleteDirectoryWithRetry(dataDirectory.FullName);
        }
    }

    private sealed record HealthDto(string Status, string Detail);

    private static void DeleteDirectoryWithRetry(string path)
    {
        SqliteConnection.ClearAllPools();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
        }

        Directory.Delete(path, recursive: true);
    }

    private sealed class FakeEngineHealthService(EngineHealthResult result) : IEngineHealthService
    {
        public Task<EngineHealthResult> CheckReadinessAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
