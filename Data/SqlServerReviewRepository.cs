using System.Data;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerReviewRepository : IReviewRepository, IMultiReviewRepository
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

    public async Task<IReadOnlyList<UserReview>> GetMineAllAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, UserId, PropertyId, Rating, Comment, CreatedAt
            FROM dbo.Review
            WHERE UserId = @userId AND PropertyId = @propertyId
            ORDER BY CreatedAt DESC;
            """;
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddIds(command, userId, propertyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var reviews = new List<UserReview>();
        while (await reader.ReadAsync(cancellationToken)) reviews.Add(ReadReview(reader));
        return reviews;
    }

    public async Task<ReviewUpsertResult> CreateAsync(
        Guid userId,
        Guid propertyId,
        int rating,
        string? comment,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Review (Id, UserId, PropertyId, Rating, Comment, CreatedAt)
            OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.PropertyId,
                   INSERTED.Rating, INSERTED.Comment, INSERTED.CreatedAt
            SELECT @id, @userId, @propertyId, @rating, @comment, SYSUTCDATETIME()
            WHERE EXISTS (SELECT 1 FROM dbo.Property WHERE Id = @propertyId);
            """;
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddIds(command, userId, propertyId);
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@rating", SqlDbType.Int).Value = rating;
        command.Parameters.Add("@comment", SqlDbType.NVarChar, 1000).Value = comment is null ? DBNull.Value : comment;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReviewUpsertResult.Success(ReadReview(reader))
            : ReviewUpsertResult.PropertyNotFound();
    }

    public async Task<UserReview?> UpdateMineAsync(
        Guid userId,
        Guid reviewId,
        int rating,
        string? comment,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Review
            SET Rating = @rating, Comment = @comment
            OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.PropertyId,
                   INSERTED.Rating, INSERTED.Comment, INSERTED.CreatedAt
            WHERE Id = @reviewId AND UserId = @userId;
            """;
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@reviewId", SqlDbType.UniqueIdentifier).Value = reviewId;
        command.Parameters.Add("@rating", SqlDbType.Int).Value = rating;
        command.Parameters.Add("@comment", SqlDbType.NVarChar, 1000).Value = comment is null ? DBNull.Value : comment;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadReview(reader) : null;
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
