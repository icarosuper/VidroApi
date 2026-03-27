using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public class User : BaseEntity
{
    [ExcludeFromCodeCoverage]
    private User() { }

    public User(string username, string email, string passwordHash, DateTimeOffset now)
        : base(now)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
    }

    public string Username { get; init; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;

    public void ChangeEmail(string email) => Email = email;
    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    // Navigation properties added as entities are implemented:
    // private List<Channel> _channels = [];
    // public IReadOnlyList<Channel> Channels => _channels.AsReadOnly();
}
