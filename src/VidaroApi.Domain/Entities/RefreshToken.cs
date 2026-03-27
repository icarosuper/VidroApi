using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public class RefreshToken : BaseEntity
{
    [ExcludeFromCodeCoverage]
    private RefreshToken() { }

    public RefreshToken(Guid userId, string token, DateTimeOffset expiresAt, DateTimeOffset now)
        : base(now)
    {
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; init; }
    public string Token { get; init; } = null!;
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsRevoked { get; private set; } = false;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsValid(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    public void Revoke() => IsRevoked = true;

    // Navigation property
    public User User { get; init; } = null!;
}
