using System.Data;
using System.Text;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerProductRepository : IProductRepository
{
    private const string CheckInPolicy = "Check-in from 14:00. Please bring a valid ID.";
    private const string CheckOutPolicy = "Check-out before 12:00.";
    private const string CancellationPolicy = "Free cancellation up to 24 hours before check-in.";

    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerProductRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProductListItem>> GetAllAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder("""
            SELECT
                p.Id AS id,
                COALESCE(pt.[Name], N'Stay') AS type,
                p.[Name] AS name,
                p.[Description] AS description,
                p.City AS city,
                p.[Address] AS address,
                image.ImageUrl AS imageurl,
                COALESCE(room_price.PricePerNight, 0) AS pricepernight,
                COALESCE(review_summary.Rating, 0) AS rating,
                COALESCE(review_summary.ReviewCount, 0) AS reviewcount,
                CASE
                    WHEN COALESCE(available_room.AvailableCount, 0) > 0 THEN N'Available'
                    ELSE N'SoldOut'
                END AS status,
                CAST(CASE
                    WHEN COALESCE(available_room.AvailableCount, 0) > 0 THEN 1
                    ELSE 0
                END AS bit) AS isavailable,
                p.HasWifi AS haswifi,
                p.HasPool AS haspool,
                p.HasParking AS hasparking,
                p.HasAC AS hasac,
                p.HasBreakfast AS hasbreakfast,
                p.IsPetAllowed AS ispetallowed,
                p.IsSelfCheckIn AS isselfcheckin
            FROM dbo.Property p
            LEFT JOIN dbo.PropertyType pt ON pt.Id = p.TypeId
            OUTER APPLY (
                SELECT TOP (1) pi.ImageUrl
                FROM dbo.PropertyImage pi
                WHERE pi.PropertyId = p.Id
                ORDER BY pi.Id
            ) image
            OUTER APPLY (
                SELECT MIN(r.Price) AS PricePerNight
                FROM dbo.Room r
                WHERE r.PropertyId = p.Id
                  AND (@guests IS NULL OR r.Capacity >= @guests)
            ) room_price
            OUTER APPLY (
                SELECT AVG(CAST(rv.Rating AS float)) AS Rating,
                       COUNT(*) AS ReviewCount
                FROM dbo.Review rv
                WHERE rv.PropertyId = p.Id
            ) review_summary
            OUTER APPLY (
                SELECT COALESCE(SUM(
                    CASE
                        WHEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0) > 0
                            THEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0)
                        ELSE 0
                    END), 0) AS AvailableCount
                FROM dbo.Room r
                OUTER APPLY (
                    SELECT COALESCE(SUM(COALESCE(b.RoomQuantity, 1)), 0) AS BookedRooms
                    FROM dbo.Booking b
                    WHERE b.RoomId = r.Id
                      AND (@checkIn IS NULL OR @checkOut IS NULL
                           OR (b.CheckIn < @checkOut AND b.CheckOut > @checkIn))
                      AND b.[Status] NOT IN (N'Cancelled', N'Canceled')
                ) booked
                WHERE r.PropertyId = p.Id
                  AND (@guests IS NULL OR r.Capacity >= @guests)
            ) available_room
            WHERE p.[Status] = N'Active'
            """);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        AddParameters(command, criteria);
        sql.AppendLine();
        AppendFilters(sql, criteria);
        sql.AppendLine();
        sql.AppendLine("ORDER BY p.CreatedAt DESC, p.[Name];");
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
                p.Id AS id,
                COALESCE(pt.[Name], N'Stay') AS type,
                p.[Name] AS name,
                p.[Description] AS description,
                p.City AS city,
                p.[Address] AS address,
                image.ImageUrl AS imageurl,
                COALESCE(room_price.PricePerNight, 0) AS pricepernight,
                COALESCE(review_summary.Rating, 0) AS rating,
                COALESCE(review_summary.ReviewCount, 0) AS reviewcount,
                CASE
                    WHEN COALESCE(available_room.AvailableCount, 0) > 0 THEN N'Available'
                    ELSE N'SoldOut'
                END AS status,
                CAST(CASE
                    WHEN COALESCE(available_room.AvailableCount, 0) > 0 THEN 1
                    ELSE 0
                END AS bit) AS isavailable,
                p.HasWifi AS haswifi,
                p.HasPool AS haspool,
                p.HasParking AS hasparking,
                p.HasAC AS hasac,
                p.HasBreakfast AS hasbreakfast,
                p.IsPetAllowed AS ispetallowed,
                p.IsSelfCheckIn AS isselfcheckin
            FROM dbo.Property p
            LEFT JOIN dbo.PropertyType pt ON pt.Id = p.TypeId
            OUTER APPLY (
                SELECT TOP (1) pi.ImageUrl
                FROM dbo.PropertyImage pi
                WHERE pi.PropertyId = p.Id
                ORDER BY pi.Id
            ) image
            OUTER APPLY (
                SELECT MIN(r.Price) AS PricePerNight
                FROM dbo.Room r
                WHERE r.PropertyId = p.Id
                  AND (@guests IS NULL OR r.Capacity >= @guests)
            ) room_price
            OUTER APPLY (
                SELECT AVG(CAST(rv.Rating AS float)) AS Rating,
                       COUNT(*) AS ReviewCount
                FROM dbo.Review rv
                WHERE rv.PropertyId = p.Id
            ) review_summary
            OUTER APPLY (
                SELECT COALESCE(SUM(
                    CASE
                        WHEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0) > 0
                            THEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0)
                        ELSE 0
                    END), 0) AS AvailableCount
                FROM dbo.Room r
                OUTER APPLY (
                    SELECT COALESCE(SUM(COALESCE(b.RoomQuantity, 1)), 0) AS BookedRooms
                    FROM dbo.Booking b
                    WHERE b.RoomId = r.Id
                      AND (@checkIn IS NULL OR @checkOut IS NULL
                           OR (b.CheckIn < @checkOut AND b.CheckOut > @checkIn))
                      AND b.[Status] NOT IN (N'Cancelled', N'Canceled')
                ) booked
                WHERE r.PropertyId = p.Id
                  AND (@guests IS NULL OR r.Capacity >= @guests)
            ) available_room
            WHERE p.Id = @id
              AND p.[Status] = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
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
            SELECT ImageUrl AS imageurl
            FROM dbo.PropertyImage
            WHERE PropertyId = @propertyId
              AND ImageUrl IS NOT NULL
            ORDER BY Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;

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
                r.Id AS id,
                r.[Name] AS name,
                r.Capacity AS capacity,
                r.Price AS price,
                image.ImageUrl AS imageurl,
                CASE
                    WHEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0) > 0
                        THEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0)
                    ELSE 0
                END AS availablerooms
            FROM dbo.Room r
            OUTER APPLY (
                SELECT COALESCE(SUM(COALESCE(b.RoomQuantity, 1)), 0) AS BookedRooms
                FROM dbo.Booking b
                WHERE b.RoomId = r.Id
                  AND (@checkIn IS NULL OR @checkOut IS NULL
                       OR (b.CheckIn < @checkOut AND b.CheckOut > @checkIn))
                  AND b.[Status] NOT IN (N'Cancelled', N'Canceled')
            ) booked
            OUTER APPLY (
                SELECT TOP (1) ri.ImageUrl
                FROM dbo.RoomImage ri
                WHERE ri.RoomId = r.Id
                ORDER BY ri.Id
            ) image
            WHERE r.PropertyId = @propertyId
              AND (@guests IS NULL OR r.Capacity >= @guests)
              AND (CASE
                    WHEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0) > 0
                        THEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0)
                    ELSE 0
                  END) > 0
            ORDER BY r.Price, r.[Name];
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;
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
            SELECT TOP (10)
                COALESCE(u.FullName, N'BingCook guest') AS author,
                rv.Rating AS rating,
                rv.CreatedAt AS createdat,
                rv.Comment AS comment
            FROM dbo.Review rv
            LEFT JOIN dbo.[User] u ON u.Id = rv.UserId
            WHERE rv.PropertyId = @propertyId
            ORDER BY rv.CreatedAt DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;

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
                    : reader.GetString(reader.GetOrdinal("comment"))));
        }

        return reviews;
    }

    private static ProductListItem ReadProduct(SqlDataReader reader)
    {
        return new ProductListItem(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("type")),
            reader.GetString(reader.GetOrdinal("name")),
            reader.IsDBNull(reader.GetOrdinal("description"))
                ? null
                : reader.GetString(reader.GetOrdinal("description")),
            reader.IsDBNull(reader.GetOrdinal("city"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("city")),
            reader.IsDBNull(reader.GetOrdinal("address"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("address")),
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
        SqlCommand command,
        ProductSearchCriteria criteria)
    {
        AddNullable(command, "@keyword", SqlDbType.NVarChar, ToPattern(criteria.Keyword));
        AddNullable(command, "@location", SqlDbType.NVarChar, ToPattern(criteria.Location));
        AddNullable(command, "@type", SqlDbType.NVarChar, ToPattern(criteria.Type));
        AddNullable(command, "@guests", SqlDbType.Int, criteria.Guests);
        AddNullable(command, "@minPrice", SqlDbType.Decimal, criteria.MinPrice);
        AddNullable(command, "@maxPrice", SqlDbType.Decimal, criteria.MaxPrice);
        AddNullable(command, "@minRating", SqlDbType.Float, criteria.MinRating);
        AddNullable(
            command,
            "@checkIn",
            SqlDbType.Date,
            criteria.CheckIn?.ToDateTime(TimeOnly.MinValue));
        AddNullable(
            command,
            "@checkOut",
            SqlDbType.Date,
            criteria.CheckOut?.ToDateTime(TimeOnly.MinValue));
    }

    private static void AddNullable(
        SqlCommand command,
        string name,
        SqlDbType type,
        object? value)
    {
        var parameter = command.Parameters.Add(name, type);
        if (type == SqlDbType.NVarChar)
        {
            parameter.Size = -1;
        }
        else if (type == SqlDbType.Decimal)
        {
            parameter.Precision = 12;
            parameter.Scale = 2;
        }

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
                    LOWER(p.[Name]) LIKE LOWER(@keyword)
                    OR LOWER(p.[Description]) LIKE LOWER(@keyword)
                    OR LOWER(p.City) LIKE LOWER(@keyword)
                    OR LOWER(p.[Address]) LIKE LOWER(@keyword)
                    OR LOWER(pt.[Name]) LIKE LOWER(@keyword)
                )
                """);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Location))
        {
            sql.AppendLine("""
                AND (
                    LOWER(p.City) LIKE LOWER(@location)
                    OR LOWER(p.[Address]) LIKE LOWER(@location)
                )
                """);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Type))
        {
            sql.AppendLine("AND LOWER(pt.[Name]) LIKE LOWER(@type)");
        }

        if (criteria.Guests is > 0)
        {
            sql.AppendLine("AND COALESCE(available_room.AvailableCount, 0) > 0");
        }

        if (criteria.MinPrice is >= 0)
        {
            sql.AppendLine("AND COALESCE(room_price.PricePerNight, 0) >= @minPrice");
        }

        if (criteria.MaxPrice is >= 0)
        {
            sql.AppendLine("AND COALESCE(room_price.PricePerNight, 0) <= @maxPrice");
        }

        if (criteria.MinRating is >= 0)
        {
            sql.AppendLine("AND COALESCE(review_summary.Rating, 0) >= @minRating");
        }

        if (criteria.Amenities.Contains("Wi-Fi"))
        {
            sql.AppendLine("AND p.HasWifi = CAST(1 AS bit)");
        }

        if (criteria.Amenities.Contains("Pool"))
        {
            sql.AppendLine("AND p.HasPool = CAST(1 AS bit)");
        }

        if (criteria.Amenities.Contains("Parking"))
        {
            sql.AppendLine("AND p.HasParking = CAST(1 AS bit)");
        }

        if (criteria.Amenities.Contains("AC"))
        {
            sql.AppendLine("AND p.HasAC = CAST(1 AS bit)");
        }

        if (criteria.Amenities.Contains("Breakfast"))
        {
            sql.AppendLine("AND p.HasBreakfast = CAST(1 AS bit)");
        }

        if (criteria.Amenities.Contains("Pet allowed")
            || criteria.Amenities.Contains("Pet friendly"))
        {
            sql.AppendLine("AND p.IsPetAllowed = CAST(1 AS bit)");
        }

        if (criteria.Amenities.Contains("Self check-in"))
        {
            sql.AppendLine("AND p.IsSelfCheckIn = CAST(1 AS bit)");
        }
    }

    private static string? ToPattern(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"%{value.Trim()}%";
    }
}

