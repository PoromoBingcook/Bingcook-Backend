using BingCook.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace BingCook.Api.Data;

public sealed class PostgresReviewRepository : IReviewRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresReviewRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<UserReview?> GetMineAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, userid, propertyid, rating, comment, createdat
            FROM review
            WHERE userid = @userId
              AND propertyid = @propertyId
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        AddIds(command, userId, propertyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadReview(reader) : null;
    }

    public async Task<ReviewUpsertResult> UpsertAsync(
        Guid userId,
        Guid propertyId,
        int rating,
        string? comment,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO review (id, userid, propertyid, rating, comment, createdat)
            SELECT @id, @userId, @propertyId, @rating, @comment, CURRENT_TIMESTAMP
            WHERE EXISTS (SELECT 1 FROM property WHERE id = @propertyId)
            ON CONFLICT (userid, propertyid) DO UPDATE
            SET rating = EXCLUDED.rating,
                comment = EXCLUDED.comment,
                createdat = CURRENT_TIMESTAMP
            RETURNING id, userid, propertyid, rating, comment, createdat;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        AddIds(command, userId, propertyId);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, Guid.NewGuid());
        command.Parameters.AddWithValue("rating", NpgsqlDbType.Integer, rating);
        command.Parameters.AddWithValue(
            "comment",
            NpgsqlDbType.Text,
            comment is null ? DBNull.Value : comment);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ReviewUpsertResult.PropertyNotFound();
        }

        return ReviewUpsertResult.Success(ReadReview(reader));
    }

    private static void AddIds(NpgsqlCommand command, Guid userId, Guid propertyId)
    {
        command.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("propertyId", NpgsqlDbType.Uuid, propertyId);
    }

    private static UserReview ReadReview(NpgsqlDataReader reader)
    {
        return new UserReview(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetDateTime(5).ToUniversalTime());
    }
}
