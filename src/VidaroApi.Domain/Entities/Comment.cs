using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public class Comment : BaseAuditableEntity
{
    public const int ContentMaxLength = 1000;

    // ReSharper disable once UnusedMember.Local
    [ExcludeFromCodeCoverage]
    private Comment() { }

    public Comment(Guid videoId, Guid userId, string content, DateTimeOffset now)
        : base(now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (content.Length > ContentMaxLength)
            throw new ArgumentException($"Content cannot exceed {ContentMaxLength} characters.", nameof(content));

        VideoId = videoId;
        UserId = userId;
        Content = content;
        IsDeleted = false;
    }

    public Guid VideoId { get; init; }
    public Guid UserId { get; init; }
    public string Content { get; private set; } = null!;
    public bool IsDeleted { get; private set; }

    public void Edit(string content, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (content.Length > ContentMaxLength)
            throw new ArgumentException($"Content cannot exceed {ContentMaxLength} characters.", nameof(content));

        Content = content;
        SetUpdatedAt(now);
    }

    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        SetUpdatedAt(now);
    }

    // Navigation properties
    public Video Video { get; init; } = null!;
    public User User { get; init; } = null!;
}
