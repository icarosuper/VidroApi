using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Entities;
using VidroApi.Domain.Errors;
using VidroApi.Domain.Errors.EntityErrors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Channels;

public static class DeleteChannel
{
    public record Command : IRequest<UnitResult<Error>>
    {
        public Guid ChannelId { get; init; }
        public Guid UserId { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/v1/channels/{channelId:guid}", async (
            Guid channelId,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command
            {
                ChannelId = channelId,
                UserId = user.GetUserId()
            };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult();
        })
        .RequireAuthorization();

    public class Handler(AppDbContext db, IDateTimeProvider clock) : IRequestHandler<Command, UnitResult<Error>>
    {
        public async ValueTask<UnitResult<Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == cmd.ChannelId, ct);

            if (channel is null)
                return CommonErrors.NotFound(nameof(Channel), cmd.ChannelId);

            var userIsNotOwner = channel.UserId != cmd.UserId;
            if (userIsNotOwner)
                return Errors.Channel.NotOwner();

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await StageStorageCleanup(cmd.ChannelId, ct);
            await DeleteChannelData(cmd.ChannelId, ct);

            db.Channels.Remove(channel);
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return UnitResult.Success<Error>();
        }

        private async Task StageStorageCleanup(Guid channelId, CancellationToken ct)
        {
            var now = clock.UtcNow;

            var videoIds = await db.Videos
                .Where(v => v.ChannelId == channelId)
                .Select(v => v.Id)
                .ToListAsync(ct);

            foreach (var videoId in videoIds)
                db.PendingStorageCleanups.Add(new PendingStorageCleanup($"raw/{videoId}", isPrefix: false, now));

            var artifacts = await db.VideoArtifacts
                .Where(a => videoIds.Contains(a.VideoId))
                .Select(a => new { a.VideoId, a.ProcessedPath, a.PreviewPath, a.AudioPath, a.HlsPath })
                .ToListAsync(ct);

            foreach (var artifact in artifacts)
            {
                db.PendingStorageCleanups.Add(new PendingStorageCleanup(artifact.ProcessedPath, isPrefix: false, now));
                db.PendingStorageCleanups.Add(new PendingStorageCleanup(artifact.PreviewPath, isPrefix: false, now));
                db.PendingStorageCleanups.Add(new PendingStorageCleanup(artifact.AudioPath, isPrefix: false, now));
                if (artifact.HlsPath is not null)
                    db.PendingStorageCleanups.Add(new PendingStorageCleanup(artifact.HlsPath, isPrefix: true, now));
                db.PendingStorageCleanups.Add(new PendingStorageCleanup($"thumbnails/{artifact.VideoId}/", isPrefix: true, now));
            }
        }

        private async Task DeleteChannelData(Guid channelId, CancellationToken ct)
        {
            var videoIds = db.Videos.Where(v => v.ChannelId == channelId).Select(v => v.Id);
            var commentIds = db.Comments.Where(c => videoIds.Contains(c.VideoId)).Select(c => c.Id);

            await db.CommentReactions.Where(cr => commentIds.Contains(cr.CommentId)).ExecuteDeleteAsync(ct);
            await db.Comments.Where(c => videoIds.Contains(c.VideoId) && c.ParentCommentId != null).ExecuteDeleteAsync(ct);
            await db.Comments.Where(c => videoIds.Contains(c.VideoId)).ExecuteDeleteAsync(ct);
            await db.Reactions.Where(r => videoIds.Contains(r.VideoId)).ExecuteDeleteAsync(ct);
            await db.VideoArtifacts.Where(a => videoIds.Contains(a.VideoId)).ExecuteDeleteAsync(ct);
            await db.VideoMetadata.Where(m => videoIds.Contains(m.VideoId)).ExecuteDeleteAsync(ct);
            await RemoveChannelPlaylistData(channelId, ct);
            await db.Videos.Where(v => v.ChannelId == channelId).ExecuteDeleteAsync(ct);
            await db.ChannelFollowers.Where(cf => cf.ChannelId == channelId).ExecuteDeleteAsync(ct);
        }

        private async Task RemoveChannelPlaylistData(Guid channelId, CancellationToken ct)
        {
            var channelPlaylistIds = db.Playlists.Where(p => p.ChannelId == channelId).Select(p => p.Id);
            await db.PlaylistItems.Where(pi => channelPlaylistIds.Contains(pi.PlaylistId)).ExecuteDeleteAsync(ct);
            await db.Playlists.Where(p => p.ChannelId == channelId).ExecuteDeleteAsync(ct);
        }
    }
}
