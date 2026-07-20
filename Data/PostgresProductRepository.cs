using System.Text;
using BingCook.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace BingCook.Api.Data;

public sealed class PostgresProductRepository : IProductRepository
{
    private const string CheckInPolicy = "Check-in from 14:00. Please bring a valid ID.";
    private const string CheckOutPolicy = "Check-out before 12:00.";
    private const string CancellationPolicy = "Free cancellation before the check-in date.";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<ProductListItem>> GetAllAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder("""
            SELECT
                p.id,
                COALESCE(pt.name, 'Stay') AS type,
                p.name,
                p.description,
                p.city,
                p.address,
                image.imageurl,
                COALESCE(room_price.pricepernight, 0) AS pricepernight,
                COALESCE(review_summary.rating, 0) AS rating,
                COALESCE(review_summary.reviewcount, 0)::integer AS reviewcount,
                CASE
                    WHEN available_room.availablecount > 0 THEN 'Available'
                    ELSE 'SoldOut'
                END AS status,
                available_room.availablecount > 0 AS isavailable,
                p.haswifi,
                p.haspool,
                p.hasparking,
                p.hasac,
                p.hasbreakfast,
                p.ispetallowed,
                p.isselfcheckin
            FROM property p
            LEFT JOIN propertytype pt ON pt.id = p.typeid
            LEFT JOIN LATERAL (
                SELECT pi.imageurl
                FROM propertyimage pi
                WHERE pi.propertyid = p.id
                ORDER BY pi.id
                LIMIT 1
            ) image ON TRUE
            LEFT JOIN LATERAL (
                SELECT MIN(r.price) AS pricepernight
                FROM room r
                WHERE r.propertyid = p.id
                  AND (@guests IS NULL OR r.capacity >= @guests)
            ) room_price ON TRUE
            LEFT JOIN LATERAL (
                SELECT AVG(rv.rating)::double precision AS rating,
                       COUNT(*) AS reviewcount
                FROM review rv
                WHERE rv.propertyid = p.id
            ) review_summary ON TRUE
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(
                    GREATEST(
                        COALESCE(r.totalroom, 1) - COALESCE(booked.bookedrooms, 0),
                        0)), 0) AS availablecount
                FROM room r
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(COALESCE(b.roomquantity, 1)), 0)::integer AS bookedrooms
                    FROM booking b
                    WHERE b.roomid = r.id
                      AND (@checkIn IS NULL OR @checkOut IS NULL
                           OR (b.checkin < @checkOut AND b.checkout > @checkIn))
                      AND b.status::text NOT IN ('Cancelled', 'Canceled')
                ) booked ON TRUE
                WHERE r.propertyid = p.id
                  AND (@guests IS NULL OR r.capacity >= @guests)
            ) available_room ON TRUE
            WHERE p.status::text = 'Active'
            """);

        await using var command = _dataSource.CreateCommand();
        AddParameters(command, criteria);
        sql.AppendLine();
        AppendFilters(sql, criteria);
        sql.AppendLine();
        sql.AppendLine("ORDER BY p.createdat DESC, p.name;");
        command.CommandText = sql.ToString();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var products = new List<ProductListItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(ReadProduct(reader));
        }

        return products;
    }

    public async Task<ProductDetails?> GetByIdAsync(
        Guid id,
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var summary = await GetSummaryByIdAsync(id, criteria, cancellationToken);
        if (summary is null)
        {
            return null;
        }

        var images = await GetImageUrlsAsync(id, cancellationToken);
        var rooms = await GetAvailableRoomsAsync(id, criteria, cancellationToken);
        var reviews = await GetReviewsAsync(id, cancellationToken);
        var distribution = BuildRatingDistribution(reviews);

        return new ProductDetails(
            summary,
            images,
            CheckInPolicy,
            CheckOutPolicy,
            CancellationPolicy,
            rooms,
            distribution,
            reviews);
    }

    private async Task<ProductListItem?> GetSummaryByIdAsync(
        Guid id,
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id,
                COALESCE(pt.name, 'Stay') AS type,
                p.name,
                p.description,
                p.city,
                p.address,
                image.imageurl,
                COALESCE(room_price.pricepernight, 0) AS pricepernight,
                COALESCE(review_summary.rating, 0) AS rating,
                COALESCE(review_summary.reviewcount, 0)::integer AS reviewcount,
                CASE
                    WHEN available_room.availablecount > 0 THEN 'Available'
                    ELSE 'SoldOut'
                END AS status,
                available_room.availablecount > 0 AS isavailable,
                p.haswifi,
                p.haspool,
                p.hasparking,
                p.hasac,
                p.hasbreakfast,
                p.ispetallowed,
                p.isselfcheckin
            FROM property p
            LEFT JOIN propertytype pt ON pt.id = p.typeid
            LEFT JOIN LATERAL (
                SELECT pi.imageurl
                FROM propertyimage pi
                WHERE pi.propertyid = p.id
                ORDER BY pi.id
                LIMIT 1
            ) image ON TRUE
            LEFT JOIN LATERAL (
                SELECT MIN(r.price) AS pricepernight
                FROM room r
                WHERE r.propertyid = p.id
                  AND (@guests IS NULL OR r.capacity >= @guests)
            ) room_price ON TRUE
            LEFT JOIN LATERAL (
                SELECT AVG(rv.rating)::double precision AS rating,
                       COUNT(*) AS reviewcount
                FROM review rv
                WHERE rv.propertyid = p.id
            ) review_summary ON TRUE
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(
                    GREATEST(
                        COALESCE(r.totalroom, 1) - COALESCE(booked.bookedrooms, 0),
                        0)), 0) AS availablecount
                FROM room r
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(COALESCE(b.roomquantity, 1)), 0)::integer AS bookedrooms
                    FROM booking b
                    WHERE b.roomid = r.id
                      AND (@checkIn IS NULL OR @checkOut IS NULL
                           OR (b.checkin < @checkOut AND b.checkout > @checkIn))
                      AND b.status::text NOT IN ('Cancelled', 'Canceled')
                ) booked ON TRUE
                WHERE r.propertyid = p.id
                  AND (@guests IS NULL OR r.capacity >= @guests)
            ) available_room ON TRUE
            WHERE p.id = @id
              AND p.status::text = 'Active';
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        AddParameters(command, criteria);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadProduct(reader)
            : null;
    }

    private async Task<IReadOnlyList<string>> GetImageUrlsAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT imageurl
            FROM propertyimage
            WHERE propertyid = @propertyId
              AND imageurl IS NOT NULL
            ORDER BY id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("propertyId", propertyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var images = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            images.Add(reader.GetString(reader.GetOrdinal("imageurl")));
        }

        return images;
    }

    private async Task<IReadOnlyList<ProductRoomOption>> GetAvailableRoomsAsync(
        Guid propertyId,
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id,
                r.name,
                r.capacity,
                r.price,
                image.imageurl,
                GREATEST(
                    COALESCE(r.totalroom, 1) - COALESCE(booked.bookedrooms, 0),
                    0) AS availablerooms
            FROM room r
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(COALESCE(b.roomquantity, 1)), 0)::integer AS bookedrooms
                FROM booking b
                WHERE b.roomid = r.id
                  AND (@checkIn IS NULL OR @checkOut IS NULL
                       OR (b.checkin < @checkOut AND b.checkout > @checkIn))
                  AND b.status::text NOT IN ('Cancelled', 'Canceled')
            ) booked ON TRUE
            LEFT JOIN LATERAL (
                SELECT ri.imageurl
                FROM roomimage ri
                WHERE ri.roomid = r.id
                ORDER BY ri.id
                LIMIT 1
            ) image ON TRUE
            WHERE r.propertyid = @propertyId
              AND (@guests IS NULL OR r.capacity >= @guests)
              AND GREATEST(
                  COALESCE(r.totalroom, 1) - COALESCE(booked.bookedrooms, 0),
                  0) > 0
            ORDER BY r.price, r.name;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("propertyId", propertyId);
        AddParameters(command, criteria);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rooms = new List<ProductRoomOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var capacity = reader.GetInt32(reader.GetOrdinal("capacity"));
            rooms.Add(new ProductRoomOption(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("name")),
                capacity,
                reader.GetInt32(reader.GetOrdinal("availablerooms")),
                reader.GetDecimal(reader.GetOrdinal("price")),
                reader.IsDBNull(reader.GetOrdinal("imageurl"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("imageurl")),
                BuildRoomFeatures(capacity),
                "Instant Booking"));
        }

        return rooms;
    }

    private async Task<IReadOnlyList<ProductReview>> GetReviewsAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                rv.id,
                COALESCE(u.fullname, 'BingCook guest') AS author,
                rv.rating,
                rv.createdat,
                rv.comment
            FROM review rv
            LEFT JOIN "User" u ON u.id = rv.userid
            WHERE rv.propertyid = @propertyId
            ORDER BY rv.createdat DESC
            LIMIT 10;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("propertyId", propertyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var reviews = new List<ProductReview>();
        while (await reader.ReadAsync(cancellationToken))
        {
            reviews.Add(new ProductReview(
                reader.GetString(reader.GetOrdinal("author")),
                reader.GetInt32(reader.GetOrdinal("rating")),
                reader.GetDateTime(reader.GetOrdinal("createdat")),
                reader.IsDBNull(reader.GetOrdinal("comment"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("comment")),
                reader.GetGuid(reader.GetOrdinal("id"))));
        }

        return reviews;
    }

    private static ProductListItem ReadProduct(NpgsqlDataReader reader)
    {
        return new ProductListItem(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("type")),
            reader.GetString(reader.GetOrdinal("name")),
            reader.IsDBNull(reader.GetOrdinal("description"))
                ? null
                : reader.GetString(reader.GetOrdinal("description")),
            reader.GetString(reader.GetOrdinal("city")),
            reader.GetString(reader.GetOrdinal("address")),
            reader.IsDBNull(reader.GetOrdinal("imageurl"))
                ? null
                : reader.GetString(reader.GetOrdinal("imageurl")),
            reader.GetDecimal(reader.GetOrdinal("pricepernight")),
            reader.GetDouble(reader.GetOrdinal("rating")),
            reader.GetInt32(reader.GetOrdinal("reviewcount")),
            reader.GetString(reader.GetOrdinal("status")),
            reader.GetBoolean(reader.GetOrdinal("isavailable")),
            reader.GetBoolean(reader.GetOrdinal("haswifi")),
            reader.GetBoolean(reader.GetOrdinal("haspool")),
            reader.GetBoolean(reader.GetOrdinal("hasparking")),
            reader.GetBoolean(reader.GetOrdinal("hasac")),
            reader.GetBoolean(reader.GetOrdinal("hasbreakfast")),
            reader.GetBoolean(reader.GetOrdinal("ispetallowed")),
            reader.GetBoolean(reader.GetOrdinal("isselfcheckin")));
    }

    private static IReadOnlyList<string> BuildRoomFeatures(int capacity)
    {
        var features = new List<string>
        {
            capacity == 1 ? "1 guest" : $"Up to {capacity} guests",
            "Private room",
            "Air conditioning"
        };

        return features;
    }

    private static IReadOnlyList<ProductRatingBreakdown> BuildRatingDistribution(
        IReadOnlyList<ProductReview> reviews)
    {
        if (reviews.Count == 0)
        {
            return Enumerable
                .Range(1, 5)
                .Reverse()
                .Select(stars => new ProductRatingBreakdown(stars, 0))
                .ToList();
        }

        return Enumerable
            .Range(1, 5)
            .Reverse()
            .Select(stars =>
            {
                var count = reviews.Count(review => review.Rating == stars);
                return new ProductRatingBreakdown(stars, (double)count / reviews.Count);
            })
            .ToList();
    }

    private static void AddParameters(
        NpgsqlCommand command,
        ProductSearchCriteria criteria)
    {
        AddNullable(command, "keyword", NpgsqlDbType.Text, ToPattern(criteria.Keyword));
        AddNullable(command, "location", NpgsqlDbType.Text, ToPattern(criteria.Location));
        AddNullable(command, "type", NpgsqlDbType.Text, ToPattern(criteria.Type));
        AddNullable(command, "guests", NpgsqlDbType.Integer, criteria.Guests);
        AddNullable(command, "minPrice", NpgsqlDbType.Numeric, criteria.MinPrice);
        AddNullable(command, "maxPrice", NpgsqlDbType.Numeric, criteria.MaxPrice);
        AddNullable(command, "minRating", NpgsqlDbType.Double, criteria.MinRating);
        AddNullable(
            command,
            "checkIn",
            NpgsqlDbType.Date,
            criteria.CheckIn?.ToDateTime(TimeOnly.MinValue));
        AddNullable(
            command,
            "checkOut",
            NpgsqlDbType.Date,
            criteria.CheckOut?.ToDateTime(TimeOnly.MinValue));
    }

    private static void AddNullable(
        NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        object? value)
    {
        var parameter = command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
    }

    private static void AppendFilters(
        StringBuilder sql,
        ProductSearchCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            sql.AppendLine("""
                AND (
                    p.name ILIKE @keyword
                    OR p.description ILIKE @keyword
                    OR p.city ILIKE @keyword
                    OR p.address ILIKE @keyword
                    OR pt.name ILIKE @keyword
                )
                """);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Location))
        {
            sql.AppendLine("""
                AND (
                    p.city ILIKE @location
                    OR p.address ILIKE @location
                )
                """);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Type))
        {
            sql.AppendLine("AND pt.name ILIKE @type");
        }

        if (criteria.Guests is > 0)
        {
            sql.AppendLine("AND available_room.availablecount > 0");
        }

        if (criteria.MinPrice is >= 0)
        {
            sql.AppendLine("AND COALESCE(room_price.pricepernight, 0) >= @minPrice");
        }

        if (criteria.MaxPrice is >= 0)
        {
            sql.AppendLine("AND COALESCE(room_price.pricepernight, 0) <= @maxPrice");
        }

        if (criteria.MinRating is >= 0)
        {
            sql.AppendLine("AND COALESCE(review_summary.rating, 0) >= @minRating");
        }

        if (criteria.Amenities.Contains("Wi-Fi"))
        {
            sql.AppendLine("AND p.haswifi = TRUE");
        }

        if (criteria.Amenities.Contains("Pool"))
        {
            sql.AppendLine("AND p.haspool = TRUE");
        }

        if (criteria.Amenities.Contains("Parking"))
        {
            sql.AppendLine("AND p.hasparking = TRUE");
        }

        if (criteria.Amenities.Contains("AC"))
        {
            sql.AppendLine("AND p.hasac = TRUE");
        }

        if (criteria.Amenities.Contains("Breakfast"))
        {
            sql.AppendLine("AND p.hasbreakfast = TRUE");
        }

        if (criteria.Amenities.Contains("Pet allowed")
            || criteria.Amenities.Contains("Pet friendly"))
        {
            sql.AppendLine("AND p.ispetallowed = TRUE");
        }

        if (criteria.Amenities.Contains("Self check-in"))
        {
            sql.AppendLine("AND p.isselfcheckin = TRUE");
        }
    }

    private static string? ToPattern(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"%{value.Trim()}%";
    }
}
