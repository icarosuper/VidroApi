using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using VidroApi.Infrastructure.Persistence;

#pragma warning disable CS0618

namespace VidroApi.IntegrationTests.Common;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vidroapi_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
        builder.UseSetting("Jwt:Secret", "test-secret-that-is-long-enough-for-jwt-hmac-validation");
        builder.UseSetting("Jwt:AccessTokenExpiryMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenExpiryDays", "7");
        builder.UseSetting("MinIO:Endpoint", "localhost:9000");
        builder.UseSetting("MinIO:AccessKey", "test-access-key");
        builder.UseSetting("MinIO:SecretKey", "test-secret-key");
        builder.UseSetting("MinIO:BucketName", "test-bucket");
        builder.UseSetting("MinIO:UploadUrlTtlHours", "1");
        builder.UseSetting("Webhook:Secret", "test-webhook-secret");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await MigrateDatabase();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    private async Task MigrateDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
