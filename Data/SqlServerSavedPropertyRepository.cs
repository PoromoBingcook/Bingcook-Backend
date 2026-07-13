using System.Data;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerSavedPropertyRepository : ISavedPropertyRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerSavedPropertyRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Guid>> GetPropertyIdsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT PropertyId
            FROM dbo.SavedProperty
            WHERE UserId = @userId
            ORDER BY CreatedAt DESC, PropertyId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;

        var propertyIds = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            propertyIds.Add(reader.GetGuid(0));
        }

        return propertyIds;
    }

    public async Task<bool> SaveAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.Property
                WHERE Id = @propertyId
                  AND [Status] = N'Active'
            )
            BEGIN
                SELECT CAST(0 AS bit);
                RETURN;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM dbo.SavedProperty
                WHERE UserId = @userId
                  AND PropertyId = @propertyId
            )
            BEGIN
                INSERT INTO dbo.SavedProperty (UserId, PropertyId)
                VALUES (@userId, @propertyId);
            END;

            SELECT CAST(1 AS bit);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;

        try
        {
            return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            return true;
        }
    }

    public async Task RemoveAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM dbo.SavedProperty
            WHERE UserId = @userId
              AND PropertyId = @propertyId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
