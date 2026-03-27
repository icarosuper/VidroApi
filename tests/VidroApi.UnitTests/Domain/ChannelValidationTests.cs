using FluentAssertions;
using VidroApi.Domain.Entities;

namespace VidroApi.UnitTests.Domain;

public class ChannelValidationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenNameIsNullOrWhiteSpace(string name)
    {
        var act = () => new Channel(UserId, name, null, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNameExceedsMaxLength()
    {
        var name = new string('a', Channel.NameMaxLength + 1);
        var act = () => new Channel(UserId, name, null, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDescriptionExceedsMaxLength()
    {
        var description = new string('a', Channel.DescriptionMaxLength + 1);
        var act = () => new Channel(UserId, "My Channel", description, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrow_WhenNameExceedsMaxLength()
    {
        var channel = new Channel(UserId, "My Channel", null, Now);
        var name = new string('a', Channel.NameMaxLength + 1);

        var act = () => channel.UpdateDetails(name, null, Now.AddDays(1));

        act.Should().Throw<ArgumentException>();
    }
}
