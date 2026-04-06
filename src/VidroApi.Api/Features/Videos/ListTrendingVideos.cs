using CSharpFunctionalExtensions;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Enums;
using VidroApi.Domain.Errors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.Features.Videos;

public static class ListTrendingVideos
{
    private static readonly TimeSpan ThumbnailUrlTtl = TimeSpan.FromHours(1);

    public record Command : IRequest<Result<Response, Error>>
    {
        public int Limit { get; init; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOptions<ListTrendingVideosSettings> options)
        {
            RuleFor(x => x.Limit)
                .InclusiveBetween(1, options.Value.MaxLimit)
                .WithMessage(x => $"Limit must be between 1 and {options.Value.MaxLimit}.");
        }
    }

    public record Response
    {
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
            public List<string> ThumbnailUrls { get; init; } = [];
            public DateTimeOffset CreatedAt { get; init; }
        }

        public List<VideoSummary> Videos { get; init; } = [];
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/videos/trending", async (
            IMediator mediator,
            int limit,
            CancellationToken ct = default) =>
        {
            var cmd = new Command { Limit = limit };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        });

    public class Handler(AppDbContext db, IMinioService minio, IDateTimeProvider clock, IOptions<TrendingSettings> trendingOptions)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        private readonly TrendingSettings _trending = trendingOptions.Value;

        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var windowStart = clock.UtcNow.AddHours(-_trending.WindowHours);
            var videos = await FetchTrendingVideos(windowStart, cmd.Limit, ct);

            var thumbnailUrlLists = await GetThumbnails(videos);
            var summaries = GetSummaries(videos, thumbnailUrlLists);

            return new Response
            {
                Videos = summaries
            };
        }

        private async Task<List<List<string>>> GetThumbnails(List<Domain.Entities.Video> videos)
        {
            var thumbs = await Task.WhenAll(videos.Select(GenerateThumbnailUrls));
            return thumbs.ToList();
        }

        private static List<Response.VideoSummary> GetSummaries(List<Domain.Entities.Video> videos, List<List<string>> thumbnailUrlLists)
        {
            return videos.Select((v, i) => MapToSummary(v, thumbnailUrlLists[i])).ToList();
        }

        private static Response.VideoSummary MapToSummary(Domain.Entities.Video video, List<string> thumbnailUrls)
        {
            return new Response.VideoSummary
            {
                VideoId = video.Id,
                ChannelId = video.ChannelId,
                ChannelName = video.Channel.Name,
                Title = video.Title,
                Description = video.Description,
                Tags = video.Tags,
                ViewCount = video.ViewCount,
                LikeCount = video.LikeCount,
                ThumbnailUrls = thumbnailUrls,
                CreatedAt = video.CreatedAt
            };
        }

        private Task<List<Domain.Entities.Video>> FetchTrendingVideos(
            DateTimeOffset windowStart, int limit, CancellationToken ct)
        {
            const int readyStatus = (int)VideoStatus.Ready;
            const int publicVisibility = (int)VideoVisibility.Public;

            FormattableString sql = $"""
                       SELECT * FROM videos
                       WHERE status = {readyStatus}
                         AND visibility = {publicVisibility}
                         AND created_at > {windowStart}
                       ORDER BY (view_count * {_trending.ViewCountWeight} + like_count * {_trending.LikeCountWeight})
                              / POWER(EXTRACT(EPOCH FROM NOW() - created_at) / 3600.0 + 2, {_trending.TimeDecayFactor}) DESC
                       """;
            
            return db.Videos
                .FromSqlInterpolated(sql)
                .Include(v => v.Channel)
                .Include(v => v.Artifacts)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        private async Task<List<string>> GenerateThumbnailUrls(Domain.Entities.Video video)
        {
            if (video.Artifacts is null)
                return [];

            var paths = new List<string>();
            if (video.Artifacts.CustomThumbnailPath is not null)
                paths.Add(video.Artifacts.CustomThumbnailPath);
            paths.AddRange(video.Artifacts.ThumbnailPaths);

            var urls = await Task.WhenAll(paths.Select(p => minio.GenerateDownloadUrlAsync(p, ThumbnailUrlTtl)));
            return [..urls];
        }
    }
}
