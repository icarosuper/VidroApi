using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Entities;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.Features.Channels;

public static class GetChannel
{
    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid ChannelId { get; init; }
    }

    public record Response
    {
        public Guid ChannelId { get; init; }
        public string Name { get; init; } = null!;
        public string? Description { get; init; }
        public int FollowerCount { get; init; }
        public Guid OwnerId { get; init; }
        public string OwnerUsername { get; init; } = null!;
        public string? AvatarUrl { get; init; }
        public string? OwnerAvatarUrl { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/channels/{channelId:guid}", async (
            Guid channelId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command { ChannelId = channelId };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        });

    public class Handler(
        AppDbContext db,
        IMinioService minio,
        IOptions<MinioSettings> minioOptions)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        private readonly TimeSpan _avatarUrlTtl = TimeSpan.FromHours(minioOptions.Value.ThumbnailUrlTtlHours);

        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channel = await db.Channels
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == cmd.ChannelId, ct);

            if (channel is null)
                return CommonErrors.NotFound(nameof(Channel), cmd.ChannelId);

            var avatarUrl = await GenerateAvatarUrl(channel.AvatarPath);
            var ownerAvatarUrl = await GenerateAvatarUrl(channel.User.AvatarPath);

            return new Response
            {
                ChannelId = channel.Id,
                Name = channel.Name,
                Description = channel.Description,
                FollowerCount = channel.FollowerCount,
                OwnerId = channel.UserId,
                OwnerUsername = channel.User.Username,
                AvatarUrl = avatarUrl,
                OwnerAvatarUrl = ownerAvatarUrl
            };
        }

        private async Task<string?> GenerateAvatarUrl(string? path)
        {
            if (path is null)
                return null;

            return await minio.GenerateDownloadUrlAsync(path, _avatarUrlTtl);
        }
    }
}
