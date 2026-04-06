using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Errors;
using VidroApi.Domain.Errors.EntityErrors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.Features.Channels;

public static class UploadChannelAvatar
{
    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid ChannelId { get; init; }
        public Guid UserId { get; init; }
    }

    public record Response
    {
        public string UploadUrl { get; init; } = null!;
        public DateTimeOffset UploadExpiresAt { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/v1/channels/{channelId:guid}/avatar", async (
            Guid channelId,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command { ChannelId = channelId, UserId = user.GetUserId() };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization();

    public class Handler(
        AppDbContext db,
        IMinioService minio,
        IDateTimeProvider clock,
        IOptions<MinioSettings> minioOptions)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == cmd.ChannelId, ct);

            if (channel is null)
                return CommonErrors.NotFound(nameof(Domain.Entities.Channel), cmd.ChannelId);

            var isOwner = channel.UserId == cmd.UserId;
            if (!isOwner)
                return Errors.Channel.NotOwner();

            var objectKey = $"avatars/channels/{cmd.ChannelId}";
            var ttlHours = minioOptions.Value.UploadUrlTtlHours;
            var ttl = TimeSpan.FromHours(ttlHours);
            var uploadExpiresAt = clock.UtcNow.AddHours(ttlHours);

            var (uploadUrl, _) = await minio.GenerateUploadUrlAsync(objectKey, ttl, ct);

            channel.SetAvatar(objectKey);
            await db.SaveChangesAsync(ct);

            return new Response
            {
                UploadUrl = uploadUrl,
                UploadExpiresAt = uploadExpiresAt
            };
        }
    }
}
