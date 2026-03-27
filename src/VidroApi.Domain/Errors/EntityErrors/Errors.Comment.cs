namespace VidroApi.Domain.Errors.EntityErrors;

public static partial class Errors
{
    public static class Comment
    {
        public static Error NotFound(Guid id) =>
            new("comment.not_found", $"Comment with id '{id}' was not found.", ErrorType.NotFound);

        public static Error NotOwner() =>
            new("comment.not_owner", "You do not own this comment.", ErrorType.Forbidden);

        public static Error AlreadyDeleted() =>
            new("comment.already_deleted", "The comment has already been deleted.", ErrorType.Conflict);
    }
}
