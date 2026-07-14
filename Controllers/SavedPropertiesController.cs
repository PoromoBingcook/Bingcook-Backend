using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Products;
using BingCook.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingCook.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/saved-properties")]
public sealed class SavedPropertiesController : ControllerBase
{
    private static readonly ProductSearchCriteria DefaultCriteria = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        null,
        null);

    private readonly ISavedPropertyRepository _savedPropertyRepository;
    private readonly IProductRepository _productRepository;

    public SavedPropertiesController(
        ISavedPropertyRepository savedPropertyRepository,
        IProductRepository productRepository)
    {
        _savedPropertyRepository = savedPropertyRepository;
        _productRepository = productRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductListItemResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var savedIds = await _savedPropertyRepository.GetPropertyIdsByUserIdAsync(
            userId.Value,
            cancellationToken);
        if (savedIds.Count == 0)
        {
            return Ok(Array.Empty<ProductListItemResponse>());
        }

        var products = await _productRepository.GetAllAsync(
            DefaultCriteria,
            cancellationToken);
        var productsById = products.ToDictionary(product => product.Id);
        var response = savedIds
            .Where(productsById.ContainsKey)
            .Select(id => ToResponse(productsById[id]))
            .ToList();

        return Ok(response);
    }

    [HttpPut("{propertyId:guid}")]
    public async Task<IActionResult> Save(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var saved = await _savedPropertyRepository.SaveAsync(
            userId.Value,
            propertyId,
            cancellationToken);

        return saved
            ? NoContent()
            : NotFound(new { message = "Property not found." });
    }

    [HttpDelete("{propertyId:guid}")]
    public async Task<IActionResult> Remove(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        await _savedPropertyRepository.RemoveAsync(
            userId.Value,
            propertyId,
            cancellationToken);
        return NoContent();
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? userId : null;
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
            product.Latitude,
            product.Longitude,
            product.ImageUrl,
            Math.Round(product.Rating, 1),
            product.ReviewCount,
            BuildAmenities(product),
            product.PricePerNight,
            product.IsAvailable ? "Available" : "SoldOut");
    }

    private static IReadOnlyList<string> BuildAmenities(ProductListItem product)
    {
        var amenities = new List<string>();
        if (product.HasWifi) amenities.Add("Wi-Fi");
        if (product.HasPool) amenities.Add("Pool");
        if (product.HasParking) amenities.Add("Parking");
        if (product.HasAC) amenities.Add("AC");
        if (product.HasBreakfast) amenities.Add("Breakfast");
        if (product.IsPetAllowed) amenities.Add("Pet friendly");
        if (product.IsSelfCheckIn) amenities.Add("Self check-in");
        return amenities;
    }
}
