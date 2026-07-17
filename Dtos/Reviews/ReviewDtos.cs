namespace BingCook.Api.Dtos.Reviews;

public sealed record UpsertReviewRequest(int Rating, string? Comment);

public sealed record ReviewResponse(
    Guid Id,
    Guid PropertyId,
    int Rating,
    string? Comment,
    DateTime CreatedAt);
