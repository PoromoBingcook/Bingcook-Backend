using System.Data;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerReviewRepository : IReviewRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerReviewRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserReview?> GetMineAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) Id, UserId, PropertyId, Rating, Comment, CreatedAt
            FROM dbo.Review
            WHERE UserId = @userId
              AND PropertyId = @propertyId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
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
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            IF NOT EXISTS (SELECT 1 FROM dbo.Property WHERE Id = @propertyId)
            BEGIN
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE dbo.Review WITH (UPDLOCK, SERIALIZABLE)
            SET Rating = @rating,
                Comment = @comment,
                CreatedAt = SYSUTCDATETIME()
            WHERE UserId = @userId
              AND PropertyId = @propertyId;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.Review (Id, UserId, PropertyId, Rating, Comment, CreatedAt)
                VALUES (@id, @userId, @propertyId, @rating, @comment, SYSUTCDATETIME());
            END;

            SELECT Id, UserId, PropertyId, Rating, Comment, CreatedAt
            FROM dbo.Review
            WHERE UserId = @userId
              AND PropertyId = @propertyId;

            COMMIT TRANSACTION;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddIds(command, userId, propertyId);
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@rating", SqlDbType.Int).Value = rating;
        command.Parameters.Add("@comment", SqlDbType.NVarChar, 1000).Value =
            comment is null ? DBNull.Value : comment;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ReviewUpsertResult.PropertyNotFound();
        }

        return ReviewUpsertResult.Success(ReadReview(reader));
    }

    private static void AddIds(SqlCommand command, Guid userId, Guid propertyId)
    {
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;
    }

    private static UserReview ReadReview(SqlDataReader reader)
    {
        return new UserReview(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc));
    }
}
