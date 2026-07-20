using BingCook.Api.Controllers;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Products;
using BingCook.Api.Models;
using Microsoft.AspNetCore.Mvc;
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
    public async Task GetById_ReturnsMapCoordinates()
    {
        var id = Guid.NewGuid();
        var details = new ProductDetails(
            new ProductListItem(
                id,
                "Hotel",
                "Ocean Pearl Hotel",
                null,
                "Da Nang",
                "Vo Nguyen Giap",
                null,
                680000m,
                4.7,
                3,
                "Active",
                true,
                true,
                false,
                false,
                true,
                false,
                false,
                true,
                16.0544m,
                108.2022m),
            Array.Empty<string>(),
            "",
            "",
            "",
            Array.Empty<ProductRoomOption>(),
            Array.Empty<ProductRatingBreakdown>(),
            Array.Empty<ProductReview>());
        var repository = new CapturingProductRepository { Details = details };
        var controller = new ProductsController(repository);

        var action = await controller.GetById(
            id,
            new ProductSearchRequest(),
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ProductDetailsResponse>(result.Value);
        Assert.Equal(16.0544m, response.Latitude);
        Assert.Equal(108.2022m, response.Longitude);
    }

    [Fact]
    public async Task GetById_ReturnsCurrentRoomAvailability()
    {
        var id = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var details = new ProductDetails(
            new ProductListItem(
                id,
                "Hotel",
                "Ocean Pearl Hotel",
                null,
                "Da Nang",
                "Vo Nguyen Giap",
                null,
                680000m,
                4.7,
                3,
                "Active",
                true,
                true,
                false,
                false,
                true,
                false,
                false,
                true),
            Array.Empty<string>(),
            "",
            "",
            "",
            new[]
            {
                new ProductRoomOption(
                    roomId,
                    "Deluxe Room",
                    2,
                    7,
                    850000m,
                    null,
                    new[] { "AC" },
                    "Instant Booking")
            },
            Array.Empty<ProductRatingBreakdown>(),
            Array.Empty<ProductReview>());
        var repository = new CapturingProductRepository { Details = details };
        var controller = new ProductsController(repository);

        var action = await controller.GetById(
            id,
            new ProductSearchRequest(),
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ProductDetailsResponse>(result.Value);
        Assert.Equal(7, Assert.Single(response.Rooms).AvailableRooms);
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
        public ProductDetails? Details { get; init; }

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
            return Task.FromResult(Details);
        }
    }
}
