namespace BingCook.Api.Models;

public sealed record UserReview(
    Guid Id,
    Guid UserId,
    Guid PropertyId,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

public sealed record ReviewUpsertResult(UserReview? Review, bool PropertyExists)
{
    public static ReviewUpsertResult Success(UserReview review) => new(review, true);

    public static ReviewUpsertResult PropertyNotFound() => new(null, false);
}
