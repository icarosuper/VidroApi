using System.Security.Claims;
using CSharpFunctionalExtensions;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Common;
using VidroApi.Api.Extensions;
using VidroApi.Domain.Enums;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

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

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOptions<ListChannelVideosSettings> options)
        {
            RuleFor(x => x.Limit)
                .InclusiveBetween(1, options.Value.MaxLimit)
                .WithMessage(x => $"Limit must be between 1 and {options.Value.MaxLimit}.");
        }
    }

    public record Response
    {
        public List<VideoSummary> Videos { get; init; } = [];
        public DateTimeOffset? NextCursor { get; init; }
        
        public record VideoSummary
        {
            public Guid VideoId { get; init; }
            public string Title { get; init; } = null!;
            public string? Description { get; init; }
            public List<string> Tags { get; init; } = [];
            public EnumValue Visibility { get; init; } = null!;
            public EnumValue Status { get; init; } = null!;
            public int ViewCount { get; init; }
            public int LikeCount { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/channels/{channelId:guid}/videos", async (
            Guid channelId,
            ClaimsPrincipal user,
            IMediator mediator,
            DateTimeOffset? cursor,
            int limit,
            CancellationToken ct = default) =>
        {
            Guid? requestingUserId = user.Identity?.IsAuthenticated == true ? user.GetUserId() : null;
            var cmd = new Command
            {
                ChannelId = channelId,
                RequestingUserId = requestingUserId,
                Cursor = cursor,
                Limit = limit
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

            var isOwner = channel.UserId == cmd.RequestingUserId;
            var videos = await FetchChannelVideos(cmd.ChannelId, isOwner, cmd.Cursor, cmd.Limit, ct);

            var nextCursor = videos.Count == cmd.Limit
                ? videos[^1].CreatedAt
                : (DateTimeOffset?)null;

            return new Response
            {
                Videos = videos,
                NextCursor = nextCursor
            };
        }

        private Task<List<Response.VideoSummary>> FetchChannelVideos(
            Guid channelId, bool isOwner, DateTimeOffset? cursor, int limit, CancellationToken ct)
        {
            var query = db.Videos.Where(v => v.ChannelId == channelId).AsQueryable();

            if (!isOwner)
                query = query.Where(v => v.Visibility == VideoVisibility.Public
                                         && v.Status == VideoStatus.Ready);

            if (cursor.HasValue)
                query = query.Where(v => v.CreatedAt < cursor.Value);

            return query
                .OrderByDescending(v => v.CreatedAt)
                .Take(limit)
                .Select(v => new Response.VideoSummary
                {
                    VideoId = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    Tags = v.Tags,
                    Visibility = new EnumValue { Id = (int)v.Visibility, Value = v.Visibility.ToString() },
                    Status = new EnumValue { Id = (int)v.Status, Value = v.Status.ToString() },
                    ViewCount = v.ViewCount,
                    LikeCount = v.LikeCount,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync(ct);
        }
    }
}
