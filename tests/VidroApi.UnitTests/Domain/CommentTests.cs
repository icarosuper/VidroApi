using FluentAssertions;
using VidroApi.Domain.Entities;

namespace VidroApi.UnitTests.Domain;

public class CommentTests
{
    private static readonly Guid VideoId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_ShouldSetRequiredProperties()
    {
        var comment = new Comment(VideoId, UserId, "Great video!", Now);

        comment.VideoId.Should().Be(VideoId);
        comment.UserId.Should().Be(UserId);
        comment.Content.Should().Be("Great video!");
        comment.IsDeleted.Should().BeFalse();
        comment.CreatedAt.Should().Be(Now);
        comment.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Edit_ShouldUpdateContentAndSetUpdatedAt()
    {
        var comment = new Comment(VideoId, UserId, "Original content", Now);
        var editedAt = Now.AddMinutes(10);

        comment.Edit("Edited content", editedAt);

        comment.Content.Should().Be("Edited content");
        comment.UpdatedAt.Should().Be(editedAt);
    }

    [Fact]
    public void Delete_ShouldSetIsDeletedAndSetUpdatedAt()
    {
        var comment = new Comment(VideoId, UserId, "Some content", Now);
        var deletedAt = Now.AddMinutes(5);

        comment.Delete(deletedAt);

        comment.IsDeleted.Should().BeTrue();
        comment.UpdatedAt.Should().Be(deletedAt);
    }
}
