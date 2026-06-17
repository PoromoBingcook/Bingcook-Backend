using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductListItem>> GetAllAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<ProductDetails?> GetByIdAsync(
        Guid id,
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken);
}
