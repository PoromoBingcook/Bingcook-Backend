using BingCook.Api.Data;
using BingCook.Api.Dtos.Products;
using BingCook.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BingCook.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;

    public ProductsController(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    [HttpGet]
    [HttpGet("/api/productlist")]
    public async Task<ActionResult<IReadOnlyList<ProductListItemResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        return Ok(products.Select(ToResponse));
    }

    private static ProductListItemResponse ToResponse(ProductListItem product)
    {
        return new ProductListItemResponse(
            product.Id,
            product.Type,
            product.Name,
            product.Description,
            $"{product.City}, {product.Address}",
            product.City,
            product.Address,
            product.ImageUrl,
            Math.Round(product.Rating, 1),
            product.ReviewCount,
            BuildAmenities(product),
            product.PricePerNight,
            product.Status);
    }

    private static IReadOnlyList<string> BuildAmenities(ProductListItem product)
    {
        var amenities = new List<string>();

        if (product.HasWifi)
        {
            amenities.Add("Wi-Fi");
        }

        if (product.HasPool)
        {
            amenities.Add("Pool");
        }

        if (product.HasParking)
        {
            amenities.Add("Parking");
        }

        if (product.HasAC)
        {
            amenities.Add("AC");
        }

        if (product.HasBreakfast)
        {
            amenities.Add("Breakfast");
        }

        if (product.IsPetAllowed)
        {
            amenities.Add("Pet friendly");
        }

        if (product.IsSelfCheckIn)
        {
            amenities.Add("Self check-in");
        }

        return amenities;
    }
}
