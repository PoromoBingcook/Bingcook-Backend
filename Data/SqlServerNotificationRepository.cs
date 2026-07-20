using System.Data;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerNotificationRepository : INotificationRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerNotificationRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (5)
                Id,
                UserId,
                Title,
                [Message],
                IsRead,
                CreatedAt
            FROM dbo.Notification
            WHERE UserId = @userId
            ORDER BY CreatedAt DESC, Id DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;

        var notifications = new List<UserNotification>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notifications.Add(new UserNotification(
                reader.GetGuid(reader.GetOrdinal("Id")),
                ReadNullableGuid(reader, "UserId"),
                ReadNullableString(reader, "Title") ?? string.Empty,
                ReadNullableString(reader, "Message") ?? string.Empty,
                reader.GetBoolean(reader.GetOrdinal("IsRead")),
                DateTime.SpecifyKind(
                    reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    DateTimeKind.Utc)));
        }

        return notifications;
    }

    public async Task CreateAsync(
        CreateNotificationCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Notification (UserId, Title, [Message], IsRead)
            VALUES (@userId, @title, @message, 0);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var dbCommand = new SqlCommand(sql, connection);
        dbCommand.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = command.UserId;
        AddText(dbCommand, "@title", command.Title, 200);
        AddText(dbCommand, "@message", command.Message);
        await dbCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Notification
            SET IsRead = 1
            WHERE Id = @notificationId
              AND UserId = @userId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@notificationId", SqlDbType.UniqueIdentifier).Value = notificationId;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Notification
            SET IsRead = 1
            WHERE UserId = @userId
              AND IsRead = 0;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddText(
        SqlCommand command,
        string name,
        string value,
        int size = -1)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = value;
    }

    private static Guid? ReadNullableGuid(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static string? ReadNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
