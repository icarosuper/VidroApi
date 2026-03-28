using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Extensions;
using VidroApi.Domain.Enums;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Videos;

public static class ListChannelVideos
{
    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid ChannelId { get; init; }
        public Guid? RequestingUserId { get; init; }
        public DateTimeOffset? Cursor { get; init; }
        public int Limit { get; init; }
    }

    public record VideoSummary
    {
        public Guid VideoId { get; init; }
        public string Title { get; init; } = null!;
        public string? Description { get; init; }
        public List<string> Tags { get; init; } = [];
        public string Visibility { get; init; } = null!;
        public string Status { get; init; } = null!;
        public int ViewCount { get; init; }
        public int LikeCount { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    public record Response
    {
        public List<VideoSummary> Videos { get; init; } = [];
        public DateTimeOffset? NextCursor { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/channels/{channelId:guid}/videos", async (
            Guid channelId,
            ClaimsPrincipal user,
            IMediator mediator,
            DateTimeOffset? cursor,
            int limit = 20,
            CancellationToken ct = default) =>
        {
            Guid? requestingUserId = user.Identity?.IsAuthenticated == true ? user.GetUserId() : null;
            var cmd = new Command
            {
                ChannelId = channelId,
                RequestingUserId = requestingUserId,
                Cursor = cursor,
                Limit = Math.Clamp(limit, 1, 50)
            };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        });

    public class Handler(AppDbContext db)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == cmd.ChannelId, ct);
            if (channel is null)
                return CommonErrors.NotFound(nameof(Domain.Entities.Channel), cmd.ChannelId);

            var query = db.Videos.Where(v => v.ChannelId == cmd.ChannelId).AsQueryable();

            var isOwner = channel.UserId == cmd.RequestingUserId;
            if (!isOwner)
                query = query.Where(v => v.Visibility == VideoVisibility.Public
                                         && v.Status == VideoStatus.Ready);

            if (cmd.Cursor.HasValue)
                query = query.Where(v => v.CreatedAt < cmd.Cursor.Value);

            var videos = await query
                .OrderByDescending(v => v.CreatedAt)
                .Take(cmd.Limit)
                .Select(v => new VideoSummary
                {
                    VideoId = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    Tags = v.Tags,
                    Visibility = v.Visibility.ToString(),
                    Status = v.Status.ToString(),
                    ViewCount = v.ViewCount,
                    LikeCount = v.LikeCount,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync(ct);

            var nextCursor = videos.Count == cmd.Limit ? videos[^1].CreatedAt : (DateTimeOffset?)null;

            return new Response
            {
                Videos = videos,
                NextCursor = nextCursor
            };
        }
    }
}
