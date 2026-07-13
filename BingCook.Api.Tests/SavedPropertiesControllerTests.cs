using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Controllers;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Products;
using BingCook.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class SavedPropertiesControllerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FirstPropertyId = Guid.NewGuid();
    private static readonly Guid SecondPropertyId = Guid.NewGuid();

    [Fact]
    public async Task GetMine_ReturnsOnlySavedProductsInSavedOrder()
    {
        var savedRepository = new FakeSavedPropertyRepository
        {
            PropertyIds = [SecondPropertyId, FirstPropertyId]
        };
        var productRepository = new FakeProductRepository(
            [CreateProduct(FirstPropertyId, "First"), CreateProduct(SecondPropertyId, "Second")]);
        var controller = CreateController(savedRepository, productRepository);

        var action = await controller.GetMine(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var products = Assert.IsAssignableFrom<IReadOnlyList<ProductListItemResponse>>(
            result.Value);
        Assert.Equal([SecondPropertyId, FirstPropertyId], products.Select(item => item.Id));
    }

    [Fact]
    public async Task Save_ReturnsNotFoundWhenPropertyIsUnavailable()
    {
        var savedRepository = new FakeSavedPropertyRepository { CanSave = false };
        var controller = CreateController(
            savedRepository,
            new FakeProductRepository([]));

        var result = await controller.Save(FirstPropertyId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(UserId, savedRepository.LastUserId);
        Assert.Equal(FirstPropertyId, savedRepository.LastPropertyId);
    }

    [Fact]
    public async Task Remove_UsesAuthenticatedUserAndIsIdempotent()
    {
        var savedRepository = new FakeSavedPropertyRepository();
        var controller = CreateController(
            savedRepository,
            new FakeProductRepository([]));

        var result = await controller.Remove(FirstPropertyId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(UserId, savedRepository.LastUserId);
        Assert.Equal(FirstPropertyId, savedRepository.LastPropertyId);
    }

    private static SavedPropertiesController CreateController(
        ISavedPropertyRepository savedPropertyRepository,
        IProductRepository productRepository)
    {
        var controller = new SavedPropertiesController(
            savedPropertyRepository,
            productRepository);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString())],
                        "Test"))
            }
        };
        return controller;
    }

    private static ProductListItem CreateProduct(Guid id, string name)
    {
        return new ProductListItem(
            id,
            "Hotel",
            name,
            "Description",
            "Da Nang",
            "Son Tra",
            null,
            1000000,
            4.5,
            2,
            "Available",
            true,
            true,
            false,
            true,
            true,
            false,
            false,
            true);
    }

    private sealed class FakeSavedPropertyRepository : ISavedPropertyRepository
    {
        public IReadOnlyList<Guid> PropertyIds { get; init; } = [];
        public bool CanSave { get; init; } = true;
        public Guid? LastUserId { get; private set; }
        public Guid? LastPropertyId { get; private set; }

        public Task<IReadOnlyList<Guid>> GetPropertyIdsByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            return Task.FromResult(PropertyIds);
        }

        public Task<bool> SaveAsync(
            Guid userId,
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            LastPropertyId = propertyId;
            return Task.FromResult(CanSave);
        }

        public Task RemoveAsync(
            Guid userId,
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            LastPropertyId = propertyId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly IReadOnlyList<ProductListItem> _products;

        public FakeProductRepository(IReadOnlyList<ProductListItem> products)
        {
            _products = products;
        }

        public Task<IReadOnlyList<ProductListItem>> GetAllAsync(
            ProductSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_products);
        }

        public Task<ProductDetails?> GetByIdAsync(
            Guid id,
            ProductSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ProductDetails?>(null);
        }
    }
}
