using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IReviewRepository
{
    Task<UserReview?> GetMineAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken);

    Task<ReviewUpsertResult> UpsertAsync(
        Guid userId,
        Guid propertyId,
        int rating,
        string? comment,
        CancellationToken cancellationToken);
}

public interface IMultiReviewRepository
{
    Task<IReadOnlyList<UserReview>> GetMineAllAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken);

    Task<ReviewUpsertResult> CreateAsync(
        Guid userId,
        Guid propertyId,
        int rating,
        string? comment,
        CancellationToken cancellationToken);

    Task<UserReview?> UpdateMineAsync(
        Guid userId,
        Guid reviewId,
        int rating,
        string? comment,
        CancellationToken cancellationToken);
}
