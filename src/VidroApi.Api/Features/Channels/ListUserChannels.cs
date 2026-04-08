using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Entities;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.Features.Channels;

public static class ListUserChannels
{
    public record Command : IRequest<Result<Response, Error>>
    {
        public string Username { get; init; } = null!;
    }

    public record Response
    {
        public List<ChannelSummary> Channels { get; init; } = [];

        public record ChannelSummary
        {
            public Guid ChannelId { get; init; }
            public string Handle { get; init; } = null!;
            public string Name { get; init; } = null!;
            public string? Description { get; init; }
            public int FollowerCount { get; init; }
            public string? AvatarUrl { get; init; }
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/users/{username}/channels", async (
            string username,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command { Username = username };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        });

    public class Handler(
        AppDbContext db,
        IMinioService minio,
        IOptions<MinioSettings> minioOptions)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        private readonly TimeSpan _avatarUrlTtl = TimeSpan.FromHours(minioOptions.Value.ThumbnailUrlTtlHours);

        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var userExists = await db.Users.AnyAsync(u => u.Username == cmd.Username, ct);
            if (!userExists)
                return CommonErrors.NotFound(nameof(User), cmd.Username);

            var channels = await db.Channels
                .Where(c => c.User.Username == cmd.Username)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);

            var avatarUrls = await GetAvatarUrls(channels);
            var summaries = channels.Select((c, i) => new Response.ChannelSummary
            {
                ChannelId = c.Id,
                Handle = c.Handle,
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

        private async Task<List<string?>> GetAvatarUrls(List<Channel> channels)
        {
            var urls = await Task.WhenAll(channels.Select(GenerateAvatarUrl));
            return urls.ToList();
        }

        private async Task<string?> GenerateAvatarUrl(Channel channel)
        {
            if (channel.AvatarPath is null)
                return null;

            return await minio.GenerateDownloadUrlAsync(channel.AvatarPath, _avatarUrlTtl);
        }
    }
}
