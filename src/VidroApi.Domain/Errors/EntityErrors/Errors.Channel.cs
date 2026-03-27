namespace VidroApi.Domain.Errors.EntityErrors;

public static partial class Errors
{
    public static class Channel
    {
        public static Error NotOwner() =>
            new("channel.not_owner", "You do not own this channel.", ErrorType.Forbidden);
    }
}
