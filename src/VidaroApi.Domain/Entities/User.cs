using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public class User : BaseEntity
{
    public const int UsernameMaxLength = 50;
    public const int EmailMaxLength = 255;

    // ReSharper disable once UnusedMember.Local
    [ExcludeFromCodeCoverage]
    private User() { }

    public User(string username, string email, string passwordHash, DateTimeOffset now)
        : base(now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (username.Length > UsernameMaxLength)
            throw new ArgumentException($"Username cannot exceed {UsernameMaxLength} characters.", nameof(username));
        if (email.Length > EmailMaxLength)
            throw new ArgumentException($"Email cannot exceed {EmailMaxLength} characters.", nameof(email));

        Username = username;
        Email = email;
        PasswordHash = passwordHash;
    }

    public string Username { get; init; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;

    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (email.Length > EmailMaxLength)
            throw new ArgumentException($"Email cannot exceed {EmailMaxLength} characters.", nameof(email));
        Email = email;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    // ReSharper disable once CollectionNeverUpdated.Local
    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    // ReSharper disable once CollectionNeverUpdated.Local
    private readonly List<Channel> _channels = [];
    public IReadOnlyList<Channel> Channels => _channels.AsReadOnly();
}
