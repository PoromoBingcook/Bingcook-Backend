namespace BingCook.Api.Models;

public sealed record ProductSearchCriteria(
    string? Keyword,
    string? Location,
    DateOnly? CheckIn,
    DateOnly? CheckOut,
    int? Guests,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlySet<string> Amenities,
    double? MinRating,
    string? Type)
{
    public bool HasDateRange => CheckIn is not null && CheckOut is not null;
}
