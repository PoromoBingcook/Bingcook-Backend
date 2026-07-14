using BingCook.Api.Controllers;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Products;
using BingCook.Api.Models;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class ProductsControllerTests
{
    [Theory]
    [InlineData("thanh pho ho chi minh")]
    [InlineData("Thành phố Hồ Chí Minh")]
    [InlineData("Ho Chi Minh City")]
    [InlineData("HCM")]
    [InlineData("Sài Gòn")]
    public async Task GetAll_NormalizesHoChiMinhAliases(string keyword)
    {
        var repository = new CapturingProductRepository();
        var controller = new ProductsController(repository);

        await controller.GetAll(
            new ProductSearchRequest { Keyword = keyword },
            CancellationToken.None);

        Assert.NotNull(repository.LastCriteria);
        Assert.Equal("Ho Chi Minh", repository.LastCriteria.Keyword);
    }

    [Fact]
    public async Task GetAll_LeavesHotelNameKeywordSearchUntouched()
    {
        var repository = new CapturingProductRepository();
        var controller = new ProductsController(repository);

        await controller.GetAll(
            new ProductSearchRequest { Keyword = "Ocean Pearl" },
            CancellationToken.None);

        Assert.NotNull(repository.LastCriteria);
        Assert.Equal("Ocean Pearl", repository.LastCriteria.Keyword);
    }

    private sealed class CapturingProductRepository : IProductRepository
    {
        public ProductSearchCriteria? LastCriteria { get; private set; }

        public Task<IReadOnlyList<ProductListItem>> GetAllAsync(
            ProductSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            LastCriteria = criteria;
            return Task.FromResult<IReadOnlyList<ProductListItem>>(
                Array.Empty<ProductListItem>());
        }

        public Task<ProductDetails?> GetByIdAsync(
            Guid id,
            ProductSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            LastCriteria = criteria;
            return Task.FromResult<ProductDetails?>(null);
        }
    }
}
