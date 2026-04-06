using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.Features.Channels;

public static class ListMyChannels
{
    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid UserId { get; init; }
    }

    public record Response
    {
        public List<ChannelSummary> Channels { get; init; } = [];

        public record ChannelSummary
        {
            public Guid ChannelId { get; init; }
            public string Name { get; init; } = null!;
            public string? Description { get; init; }
            public int FollowerCount { get; init; }
            public string? AvatarUrl { get; init; }
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/users/me/channels", async (
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command { UserId = user.GetUserId() };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization();

    public class Handler(
        AppDbContext db,
        IMinioService minio,
        IOptions<MinioSettings> minioOptions)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        private readonly TimeSpan _avatarUrlTtl = TimeSpan.FromHours(minioOptions.Value.ThumbnailUrlTtlHours);

        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channels = await db.Channels
                .Where(c => c.UserId == cmd.UserId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);

            var avatarUrls = await GetAvatarUrls(channels);
            var summaries = channels.Select((c, i) => new Response.ChannelSummary
            {
                ChannelId = c.Id,
                Name = c.Name,
                Description = c.Description,
                FollowerCount = c.FollowerCount,
                AvatarUrl = avatarUrls[i]
            }).ToList();

            return new Response
            {
                Channels = summaries
            };
        }

        private async Task<List<string?>> GetAvatarUrls(List<Domain.Entities.Channel> channels)
        {
            var urls = await Task.WhenAll(channels.Select(GenerateAvatarUrl));
            return urls.ToList();
        }

        private async Task<string?> GenerateAvatarUrl(Domain.Entities.Channel channel)
        {
            if (channel.AvatarPath is null)
                return null;

            return await minio.GenerateDownloadUrlAsync(channel.AvatarPath, _avatarUrlTtl);
        }
    }
}
