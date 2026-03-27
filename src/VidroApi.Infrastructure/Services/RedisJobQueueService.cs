using System.Text.Json;
using StackExchange.Redis;
using VidroApi.Application.Abstractions;

namespace VidroApi.Infrastructure.Services;

public class RedisJobQueueService(IConnectionMultiplexer redis) : IJobQueueService
{
    private const string QueueName = "PROCESSING_REQUEST_QUEUE";

    public async Task PublishJobAsync(string videoId, string callbackUrl, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();

        var jobState = new
        {
            status = "pending",
            callback_url = callbackUrl,
            retry_count = 0,
            created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updated_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await db.StringSetAsync(
            $"job:{videoId}",
            JsonSerializer.Serialize(jobState),
            TimeSpan.FromHours(24));

        await db.ListLeftPushAsync(QueueName, videoId);
    }
}
