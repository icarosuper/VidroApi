using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Extensions;
using VidroApi.Domain.Entities;
using VidroApi.Domain.Errors;
using VidroApi.Domain.Errors.EntityErrors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Channels;

public static class UnfollowChannel
{
    public record Command : IRequest<UnitResult<Error>>
    {
        public Guid ChannelId { get; init; }
        public Guid UserId { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/v1/channels/{channelId:guid}/follow", async (
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

    public class Handler(AppDbContext db) : IRequestHandler<Command, UnitResult<Error>>
    {
        public async ValueTask<UnitResult<Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == cmd.ChannelId, ct);
            if (channel is null)
                return CommonErrors.NotFound(nameof(Channel), cmd.ChannelId);

            var follower = await db.ChannelFollowers
                .FirstOrDefaultAsync(cf => cf.ChannelId == cmd.ChannelId && cf.UserId == cmd.UserId, ct);

            var notFollowing = follower is null;
            if (notFollowing)
                return Errors.Channel.NotFollowing();

            db.ChannelFollowers.Remove(follower!);
            await db.SaveChangesAsync(ct);

            await db.Channels
                .Where(c => c.Id == cmd.ChannelId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.FollowerCount, c => c.FollowerCount - 1), ct);

            return UnitResult.Success<Error>();
        }
    }
}
