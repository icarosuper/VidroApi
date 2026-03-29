using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Common;
using VidroApi.Api.Extensions;
using VidroApi.Domain.Enums;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Videos;

public static class GetVideo
{
    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid VideoId { get; init; }
        public Guid? RequestingUserId { get; init; }
    }

    public record Response
    {
        public Guid VideoId { get; init; }
        public Guid ChannelId { get; init; }
        public string ChannelName { get; init; } = null!;
        public string Title { get; init; } = null!;
        public string? Description { get; init; }
        public List<string> Tags { get; init; } = [];
        public EnumValue Visibility { get; init; } = null!;
        public EnumValue Status { get; init; } = null!;
        public int ViewCount { get; init; }
        public int LikeCount { get; init; }
        public int DislikeCount { get; init; }
        public int CommentCount { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/videos/{videoId:guid}", async (
            Guid videoId,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            Guid? requestingUserId = user.Identity?.IsAuthenticated == true
                ? user.GetUserId()
                : null;
            var cmd = new Command { VideoId = videoId, RequestingUserId = requestingUserId };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        });

    public class Handler(AppDbContext db)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var video = await FetchVideo(cmd.VideoId, cmd.RequestingUserId, ct);

            if (video is null)
                return CommonErrors.NotFound(nameof(Domain.Entities.Video), cmd.VideoId);

            return new Response
            {
                VideoId = video.Id,
                ChannelId = video.ChannelId,
                ChannelName = video.Channel.Name,
                Title = video.Title,
                Description = video.Description,
                Tags = video.Tags,
                Visibility = EnumValue.From(video.Visibility),
                Status = EnumValue.From(video.Status),
                ViewCount = video.ViewCount,
                LikeCount = video.LikeCount,
                DislikeCount = video.DislikeCount,
                CommentCount = video.CommentCount,
                CreatedAt = video.CreatedAt
            };
        }

        private Task<Domain.Entities.Video?> FetchVideo(Guid videoId, Guid? requestingUserId, CancellationToken ct)
        {
            return db.Videos
                .Include(v => v.Channel)
                .FirstOrDefaultAsync(v => v.Id == videoId
                    && (v.Channel.UserId == requestingUserId
                        || (v.Status == VideoStatus.Ready && v.Visibility != VideoVisibility.Private)),
                    ct);
        }
    }
}
