using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Common;
using VidroApi.Api.Extensions;
using VidroApi.Application.Common;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Playlists;

public static class ListPlaylistsByChannel
{
    public record Query : IRequest<Result<PagedResult<PlaylistResponse>, Error>>
    {
        public Guid ChannelId { get; init; }
        public Guid? RequestingUserId { get; init; }
        public string? Cursor { get; init; }
        public int Limit { get; init; } = 20;
    }

    public record PlaylistResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = null!;
        public string? Description { get; init; }
        public EnumValue Visibility { get; init; } = null!;
        public int VideoCount { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/channels/{channelId:guid}/playlists", async (
            Guid channelId,
            string? cursor,
            int limit,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            Guid? requestingUserId = user.Identity?.IsAuthenticated == true
                ? user.GetUserId()
                : null;

            var query = new Query
            {
                ChannelId = channelId,
                RequestingUserId = requestingUserId,
                Cursor = cursor,
                Limit = Math.Max(1, Math.Min(limit, 100))
            };

            var result = await mediator.Send(query, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        });

    public class Handler(AppDbContext db)
        : IRequestHandler<Query, Result<PagedResult<PlaylistResponse>, Error>>
    {
        public async ValueTask<Result<PagedResult<PlaylistResponse>, Error>> Handle(
            Query query, CancellationToken ct)
        {
            var playlists = await FetchPlaylists(query, ct);

            var responses = playlists
                .Select(p => new PlaylistResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Visibility = EnumValue.From(p.Visibility),
                    VideoCount = p.VideoCount,
                    CreatedAt = p.CreatedAt
                })
                .ToList();

            string? nextCursor = null;
            if (playlists.Count == query.Limit + 1)
            {
                nextCursor = playlists[query.Limit].CreatedAt.ToString("O");
                responses.RemoveAt(responses.Count - 1);
            }

            return new PagedResult<PlaylistResponse>(responses, nextCursor);
        }

        private async Task<List<Domain.Entities.Playlist>> FetchPlaylists(Query query, CancellationToken ct)
        {
            var isChannelOwner = await IsChannelOwner(query.ChannelId, query.RequestingUserId, ct);

            var dbQuery = db.Playlists
                .Where(p => p.ChannelId == query.ChannelId);

            if (!isChannelOwner)
            {
                dbQuery = dbQuery.Where(p => p.Visibility == Domain.Enums.PlaylistVisibility.Public);
            }

            if (!string.IsNullOrEmpty(query.Cursor) && DateTimeOffset.TryParse(query.Cursor, out var cursorDate))
            {
                dbQuery = dbQuery.Where(p => p.CreatedAt < cursorDate);
            }

            return await dbQuery
                .OrderByDescending(p => p.CreatedAt)
                .Take(query.Limit + 1)
                .ToListAsync(ct);
        }

        private Task<bool> IsChannelOwner(Guid channelId, Guid? requestingUserId, CancellationToken ct)
        {
            if (!requestingUserId.HasValue)
                return Task.FromResult(false);

            return db.Channels
                .Where(c => c.Id == channelId && c.UserId == requestingUserId.Value)
                .AnyAsync(ct);
        }
    }
}
