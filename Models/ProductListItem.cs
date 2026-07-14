namespace BingCook.Api.Models;

public sealed record ProductListItem(
    Guid Id,
    string Type,
    string Name,
    string? Description,
    string City,
    string Address,
    string? ImageUrl,
    decimal PricePerNight,
    double Rating,
    int ReviewCount,
    string Status,
    bool IsAvailable,
    bool HasWifi,
    bool HasPool,
    bool HasParking,
    bool HasAC,
    bool HasBreakfast,
    bool IsPetAllowed,
    bool IsSelfCheckIn,
    decimal? Latitude = null,
    decimal? Longitude = null);
