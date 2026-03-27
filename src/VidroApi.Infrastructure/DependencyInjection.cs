using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using StackExchange.Redis;
using VidroApi.Application.Abstractions;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Services;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // PostgreSQL
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Postgres")));

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(config.GetConnectionString("Redis")!));

        // MinIO
        var minioSettings = config.GetSection("MinIO").Get<MinioSettings>()!;
        services.AddMinio(client => client
            .WithEndpoint(minioSettings.Endpoint)
            .WithCredentials(minioSettings.AccessKey, minioSettings.SecretKey)
            .WithSSL(minioSettings.UseSsl));

        // Services
        services.AddScoped<IMinioService, MinioService>();
        services.AddScoped<IJobQueueService, RedisJobQueueService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}
