namespace BingCook.Api.Dtos.Products;

public sealed class ProductSearchRequest
{
    public string? Keyword { get; init; }

    public string? Location { get; init; }

    public DateOnly? CheckIn { get; init; }

    public DateOnly? CheckOut { get; init; }

    public int? Guests { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public string? Amenities { get; init; }

    public double? MinRating { get; init; }

    public string? Type { get; init; }
}
