using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    protected BaseEntity(DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        CreatedAt = createdAt;
    }

    [ExcludeFromCodeCoverage]
    protected BaseEntity() { }
}
