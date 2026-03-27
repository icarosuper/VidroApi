using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset? UpdatedAt { get; private set; } = null;

    protected BaseAuditableEntity(DateTimeOffset createdAt) : base(createdAt) { }

    [ExcludeFromCodeCoverage]
    protected BaseAuditableEntity() { }

    protected void SetUpdatedAt(DateTimeOffset now) => UpdatedAt = now;
}
