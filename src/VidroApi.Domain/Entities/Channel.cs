using System.Diagnostics.CodeAnalysis;

namespace VidroApi.Domain.Entities;

public class Channel : BaseAuditableEntity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    // ReSharper disable once UnusedMember.Local
    [ExcludeFromCodeCoverage]
    private Channel() { }

    public Channel(Guid userId, string name, string? description, DateTimeOffset now)
        : base(now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > NameMaxLength)
            throw new ArgumentException($"Name cannot exceed {NameMaxLength} characters.", nameof(name));
        if (description is { Length: > DescriptionMaxLength })
            throw new ArgumentException($"Description cannot exceed {DescriptionMaxLength} characters.", nameof(description));

        UserId = userId;
        Name = name;
        Description = description;
        FollowerCount = 0;
    }

    public Guid UserId { get; init; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int FollowerCount { get; private set; }
    public string? AvatarPath { get; private set; }

    public void UpdateDetails(string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > NameMaxLength)
            throw new ArgumentException($"Name cannot exceed {NameMaxLength} characters.", nameof(name));
        if (description is { Length: > DescriptionMaxLength })
            throw new ArgumentException($"Description cannot exceed {DescriptionMaxLength} characters.", nameof(description));

        Name = name;
        Description = description;
        SetUpdatedAt(now);
    }

    public void SetAvatar(string path, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AvatarPath = path;
        SetUpdatedAt(now);
    }

    // Navigation properties
    public User User { get; init; } = null!;

    // private List<Video> _videos = [];
    // public IReadOnlyList<Video> Videos => _videos.AsReadOnly();
}
