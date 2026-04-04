using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Extensions;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;

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

    public class Handler(AppDbContext db) : IRequestHandler<Command, Result<Response, Error>>
    {
        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channels = await db.Channels
                .Where(c => c.UserId == cmd.UserId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new Response.ChannelSummary
                {
                    ChannelId = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    FollowerCount = c.FollowerCount
                })
                .ToListAsync(ct);

            return new Response
            {
                Channels = channels
            };
        }
    }
}
