using System.Diagnostics.CodeAnalysis;

namespace VidroApi.Domain.Entities;

public class VideoArtifacts : BaseEntity
{
    public const int PathMaxLength = 500;

    // ReSharper disable once UnusedMember.Local
    [ExcludeFromCodeCoverage]
    private VideoArtifacts() { }

    public VideoArtifacts(Guid videoId, string processedPath, string previewPath,
        string? hlsPath, string audioPath, List<string> thumbnailPaths, DateTimeOffset now)
        : base(now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);
        ArgumentNullException.ThrowIfNull(thumbnailPaths);
        if (processedPath.Length > PathMaxLength)
            throw new ArgumentException($"ProcessedPath cannot exceed {PathMaxLength} characters.", nameof(processedPath));
        if (previewPath.Length > PathMaxLength)
            throw new ArgumentException($"PreviewPath cannot exceed {PathMaxLength} characters.", nameof(previewPath));
        if (hlsPath?.Length > PathMaxLength)
            throw new ArgumentException($"HlsPath cannot exceed {PathMaxLength} characters.", nameof(hlsPath));
        if (audioPath.Length > PathMaxLength)
            throw new ArgumentException($"AudioPath cannot exceed {PathMaxLength} characters.", nameof(audioPath));

        VideoId = videoId;
        ProcessedPath = processedPath;
        PreviewPath = previewPath;
        HlsPath = hlsPath;
        AudioPath = audioPath;
        ThumbnailPaths = thumbnailPaths;
    }

    public Guid VideoId { get; init; }
    public string ProcessedPath { get; init; } = null!;
    public string PreviewPath { get; init; } = null!;
    public string? HlsPath { get; init; }
    public string AudioPath { get; init; } = null!;
    public List<string> ThumbnailPaths { get; init; } = null!;

    // Navigation property
    public Video Video { get; init; } = null!;
}
