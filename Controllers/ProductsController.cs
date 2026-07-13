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
        [FromQuery] ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(
            ToCriteria(request),
            cancellationToken);
        return Ok(products.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailsResponse>> GetById(
        Guid id,
        [FromQuery] ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(
            id,
            ToCriteria(request),
            cancellationToken);

        return product is null
            ? NotFound(new { message = "Product not found." })
            : Ok(ToDetailsResponse(product));
    }

    private static ProductSearchCriteria ToCriteria(ProductSearchRequest request)
    {
        return new ProductSearchCriteria(
            Normalize(request.Keyword),
            Normalize(request.Location),
            request.CheckIn,
            request.CheckOut,
            request.Guests is > 0 ? request.Guests : null,
            request.MinPrice is >= 0 ? request.MinPrice : null,
            request.MaxPrice is >= 0 ? request.MaxPrice : null,
            ParseAmenities(request.Amenities),
            request.MinRating is >= 0 ? request.MinRating : null,
            Normalize(request.Type));
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static IReadOnlySet<string> ParseAmenities(string? amenities)
    {
        if (string.IsNullOrWhiteSpace(amenities))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return amenities
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    private static ProductDetailsResponse ToDetailsResponse(ProductDetails product)
    {
        var summary = product.Summary;
        return new ProductDetailsResponse(
            summary.Id,
            summary.Type,
            summary.Name,
            summary.Description,
            $"{summary.City}, {summary.Address}",
            summary.City,
            summary.Address,
            product.ImageUrls,
            Math.Round(summary.Rating, 1),
            summary.ReviewCount,
            BuildAmenities(summary),
            summary.PricePerNight,
            summary.IsAvailable ? "Available" : "SoldOut",
            product.CheckInPolicy,
            product.CheckOutPolicy,
            product.CancellationPolicy,
            product.Rooms.Select(ToRoomResponse).ToList(),
            product.RatingDistribution
                .Select(item => new ProductRatingBreakdownResponse(
                    item.Stars,
                    Math.Round(item.Fraction, 2)))
                .ToList(),
            product.Reviews.Select(ToReviewResponse).ToList());
    }

    private static ProductRoomResponse ToRoomResponse(ProductRoomOption room)
    {
        return new ProductRoomResponse(
            room.Id,
            room.Name,
            room.MaxGuests,
            room.PricePerNight,
            room.ImageUrl,
            room.Features,
            room.Policy);
    }

    private static ProductReviewResponse ToReviewResponse(ProductReview review)
    {
        return new ProductReviewResponse(
            review.Author,
            review.Rating,
            ToTimeAgo(review.CreatedAt),
            string.IsNullOrWhiteSpace(review.Comment)
                ? "No written comment."
                : review.Comment);
    }

    private static string ToTimeAgo(DateTime createdAt)
    {
        var days = Math.Max(0, (DateTime.UtcNow.Date - createdAt.Date).Days);
        return days switch
        {
            0 => "Today",
            1 => "1 day ago",
            < 30 => $"{days} days ago",
            < 60 => "1 month ago",
            < 365 => $"{days / 30} months ago",
            < 730 => "1 year ago",
            _ => $"{days / 365} years ago"
        };
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
