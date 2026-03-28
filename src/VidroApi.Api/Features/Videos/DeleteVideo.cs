using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Enums;
using VidroApi.Domain.Errors;
using VidroApi.Domain.Errors.EntityErrors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Videos;

public static class DeleteVideo
{
    public record Command : IRequest<UnitResult<Error>>
    {
        public Guid VideoId { get; init; }
        public Guid UserId { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/v1/videos/{videoId:guid}", async (
            Guid videoId,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command { VideoId = videoId, UserId = user.GetUserId() };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult();
        })
        .RequireAuthorization();

    public class Handler(
        AppDbContext db,
        IMinioService minio,
        ILogger<Handler> logger)
        : IRequestHandler<Command, UnitResult<Error>>
    {
        public async ValueTask<UnitResult<Error>> Handle(Command cmd, CancellationToken ct)
        {
            var video = await db.Videos
                .Include(v => v.Channel)
                .Include(v => v.Artifacts)
                .Include(v => v.Metadata)
                .FirstOrDefaultAsync(v => v.Id == cmd.VideoId, ct);

            if (video is null)
                return CommonErrors.NotFound(nameof(Domain.Entities.Video), cmd.VideoId);

            var isOwner = video.Channel.UserId == cmd.UserId;
            if (!isOwner)
                return video.Visibility == VideoVisibility.Private
                    ? CommonErrors.NotFound(nameof(Domain.Entities.Video), cmd.VideoId)
                    : Errors.Video.NotOwner();

            var reactions = await db.Reactions.Where(r => r.VideoId == cmd.VideoId).ToListAsync(ct);
            db.Reactions.RemoveRange(reactions);

            var comments = await db.Comments.Where(c => c.VideoId == cmd.VideoId).ToListAsync(ct);
            db.Comments.RemoveRange(comments);

            if (video.Artifacts is not null)
                db.VideoArtifacts.Remove(video.Artifacts);

            if (video.Metadata is not null)
                db.VideoMetadata.Remove(video.Metadata);

            db.Videos.Remove(video);
            await db.SaveChangesAsync(ct);

            await DeleteMinioObjectsAsync(video, ct);

            return UnitResult.Success<Error>();
        }

        private async Task DeleteMinioObjectsAsync(Domain.Entities.Video video, CancellationToken ct)
        {
            try
            {
                await minio.DeleteObjectAsync($"raw/{video.Id}", ct);

                if (video.Artifacts is null)
                    return;

                await minio.DeleteObjectAsync(video.Artifacts.ProcessedPath, ct);
                await minio.DeleteObjectAsync(video.Artifacts.PreviewPath, ct);
                await minio.DeleteObjectAsync(video.Artifacts.AudioPath, ct);
                await minio.DeleteObjectsByPrefixAsync(video.Artifacts.HlsPath, ct);
                await minio.DeleteObjectsByPrefixAsync($"thumbnails/{video.Id}/", ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete MinIO objects for video {VideoId}", video.Id);
            }
        }
    }
}
