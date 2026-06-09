using BingCook.Api.Models;
using Npgsql;

namespace BingCook.Api.Data;

public sealed class PostgresProductRepository : IProductRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<ProductListItem>> GetAllAsync(
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
                p.status::text AS status,
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
            ) room_price ON TRUE
            LEFT JOIN LATERAL (
                SELECT AVG(rv.rating)::double precision AS rating,
                       COUNT(*) AS reviewcount
                FROM review rv
                WHERE rv.propertyid = p.id
            ) review_summary ON TRUE
            WHERE p.status = 'Active'::property_status
            ORDER BY p.createdat DESC, p.name;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var products = new List<ProductListItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(ReadProduct(reader));
        }

        return products;
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
            reader.GetBoolean(reader.GetOrdinal("haswifi")),
            reader.GetBoolean(reader.GetOrdinal("haspool")),
            reader.GetBoolean(reader.GetOrdinal("hasparking")),
            reader.GetBoolean(reader.GetOrdinal("hasac")),
            reader.GetBoolean(reader.GetOrdinal("hasbreakfast")),
            reader.GetBoolean(reader.GetOrdinal("ispetallowed")),
            reader.GetBoolean(reader.GetOrdinal("isselfcheckin")));
    }
}
