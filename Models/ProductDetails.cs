namespace BingCook.Api.Models;

public sealed record ProductDetails(
    ProductListItem Summary,
    IReadOnlyList<string> ImageUrls,
    string CheckInPolicy,
    string CheckOutPolicy,
    string CancellationPolicy,
    IReadOnlyList<ProductRoomOption> Rooms,
    IReadOnlyList<ProductRatingBreakdown> RatingDistribution,
    IReadOnlyList<ProductReview> Reviews);

public sealed record ProductRoomOption(
    Guid Id,
    string Name,
    int MaxGuests,
    int AvailableRooms,
    decimal PricePerNight,
    string? ImageUrl,
    IReadOnlyList<string> Features,
    string Policy);

public sealed record ProductRatingBreakdown(
    int Stars,
    double Fraction);

public sealed record ProductReview(
    string Author,
    int Rating,
    DateTime CreatedAt,
    string? Comment,
    Guid Id = default);
