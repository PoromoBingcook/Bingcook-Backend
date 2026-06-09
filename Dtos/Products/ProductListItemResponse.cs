namespace BingCook.Api.Dtos.Products;

public sealed record ProductListItemResponse(
    Guid Id,
    string Type,
    string Name,
    string? Description,
    string Location,
    string City,
    string Address,
    string? ImageUrl,
    double Rating,
    int ReviewCount,
    IReadOnlyList<string> Amenities,
    decimal PricePerNight,
    string Status);
