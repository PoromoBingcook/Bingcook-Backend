using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductListItem>> GetAllAsync(
        CancellationToken cancellationToken);
}
