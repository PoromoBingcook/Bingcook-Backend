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
