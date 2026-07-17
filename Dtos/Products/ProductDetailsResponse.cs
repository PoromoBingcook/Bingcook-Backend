namespace BingCook.Api.Dtos.Products;

public sealed record ProductDetailsResponse(
    Guid Id,
    string Type,
    string Name,
    string? Description,
    string Location,
    string City,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<string> ImageUrls,
    double Rating,
    int ReviewCount,
    IReadOnlyList<string> Amenities,
    decimal PricePerNight,
    string Status,
    string CheckInPolicy,
    string CheckOutPolicy,
    string CancellationPolicy,
    IReadOnlyList<ProductRoomResponse> Rooms,
    IReadOnlyList<ProductRatingBreakdownResponse> RatingDistribution,
    IReadOnlyList<ProductReviewResponse> Reviews);

public sealed record ProductRoomResponse(
    Guid Id,
    string Name,
    int MaxGuests,
    decimal PricePerNight,
    string? ImageUrl,
    IReadOnlyList<string> Features,
    string Policy);

public sealed record ProductRatingBreakdownResponse(
    int Stars,
    double Fraction);

public sealed record ProductReviewResponse(
    string Author,
    int Rating,
    string TimeAgo,
    string Comment);
