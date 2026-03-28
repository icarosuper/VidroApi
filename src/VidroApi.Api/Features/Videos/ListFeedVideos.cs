using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Enums;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Videos;

public static class ListFeedVideos
{
    private static readonly TimeSpan ThumbnailUrlTtl = TimeSpan.FromHours(1);

    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid UserId { get; init; }
        public DateTimeOffset? Cursor { get; init; }
        public int Limit { get; init; }
    }

    public record VideoSummary
    {
        public Guid VideoId { get; init; }
        public Guid ChannelId { get; init; }
        public string ChannelName { get; init; } = null!;
        public string Title { get; init; } = null!;
        public string? Description { get; init; }
        public List<string> Tags { get; init; } = [];
        public int ViewCount { get; init; }
        public int LikeCount { get; init; }
        public string? ThumbnailUrl { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    public record Response
    {
        public List<VideoSummary> Videos { get; init; } = [];
        public DateTimeOffset? NextCursor { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/feed", async (
            ClaimsPrincipal user,
            IMediator mediator,
            DateTimeOffset? cursor,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            var cmd = new Command
            {
                UserId = user.GetUserId(),
                Cursor = cursor,
                Limit = Math.Clamp(limit, 1, 50)
            };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization();

    public class Handler(AppDbContext db, IMinioService minio)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var videos = await db.Videos
                .Include(v => v.Channel)
                .Include(v => v.Artifacts)
                .Where(v =>
                    db.ChannelFollowers.Any(cf => cf.UserId == cmd.UserId && cf.ChannelId == v.ChannelId)
                    && v.Channel.UserId != cmd.UserId
                    && v.Status == VideoStatus.Ready
                    && v.Visibility == VideoVisibility.Public
                    && (!cmd.Cursor.HasValue || v.CreatedAt < cmd.Cursor.Value))
                .OrderByDescending(v => v.CreatedAt)
                .Take(cmd.Limit)
                .ToListAsync(ct);

            var thumbnailUrlTasks = videos.Select(async v =>
                v.Artifacts?.ThumbnailPaths.Count > 0
                    ? (string?)await minio.GenerateDownloadUrlAsync(v.Artifacts.ThumbnailPaths[0], ThumbnailUrlTtl, ct)
                    : null);

            var thumbnailUrls = await Task.WhenAll(thumbnailUrlTasks);

            var summaries = videos.Select((v, i) => new VideoSummary
            {
                VideoId = v.Id,
                ChannelId = v.ChannelId,
                ChannelName = v.Channel.Name,
                Title = v.Title,
                Description = v.Description,
                Tags = v.Tags,
                ViewCount = v.ViewCount,
                LikeCount = v.LikeCount,
                ThumbnailUrl = thumbnailUrls[i],
                CreatedAt = v.CreatedAt
            }).ToList();

            var nextCursor = videos.Count == cmd.Limit ? videos[^1].CreatedAt : (DateTimeOffset?)null;

            return new Response { Videos = summaries, NextCursor = nextCursor };
        }
    }
}
