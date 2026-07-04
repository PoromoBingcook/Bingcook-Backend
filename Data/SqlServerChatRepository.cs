using System.Data;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerChatRepository : IChatRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerChatRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PropertyChatContext?> GetPropertyContextAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Id,
                [Name],
                HostId
            FROM dbo.Property
            WHERE Id = @propertyId
              AND [Status] = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PropertyChatContext(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetString(reader.GetOrdinal("Name")),
            ReadNullableGuid(reader, "HostId"));
    }

    public async Task<BookingChatContext?> GetBookingContextAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Id,
                PropertyId,
                UserId
            FROM dbo.Booking
            WHERE Id = @bookingId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = bookingId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.IsDBNull(reader.GetOrdinal("PropertyId"))
            || reader.IsDBNull(reader.GetOrdinal("UserId")))
        {
            return null;
        }

        return new BookingChatContext(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("PropertyId")),
            reader.GetGuid(reader.GetOrdinal("UserId")));
    }

    public async Task<ChatConversation?> FindOpenConversationAsync(
        Guid propertyId,
        Guid customerUserId,
        Guid? bookingId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                c.Id,
                c.PropertyId,
                p.[Name] AS PropertyName,
                c.BookingId,
                c.CustomerUserId,
                u.FullName AS CustomerName,
                p.HostId AS HostUserId,
                c.[Status],
                c.LastMessageAt,
                c.CustomerLastReadAt,
                c.HostLastReadAt,
                c.CreatedAt,
                c.UpdatedAt
            FROM dbo.ChatConversation c
            INNER JOIN dbo.Property p ON p.Id = c.PropertyId
            INNER JOIN dbo.[User] u ON u.Id = c.CustomerUserId
            WHERE c.PropertyId = @propertyId
              AND c.CustomerUserId = @customerUserId
              AND c.[Status] = N'Open'
              AND (
                  (@bookingId IS NULL AND c.BookingId IS NULL)
                  OR c.BookingId = @bookingId
              )
            ORDER BY c.UpdatedAt DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;
        command.Parameters.Add("@customerUserId", SqlDbType.UniqueIdentifier).Value = customerUserId;
        AddNullableGuid(command, "@bookingId", bookingId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadConversation(reader)
            : null;
    }

    public async Task<ChatConversation> CreateConversationAsync(
        Guid propertyId,
        Guid? bookingId,
        Guid customerUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.ChatConversation (
                PropertyId,
                BookingId,
                CustomerUserId,
                CustomerLastReadAt,
                HostLastReadAt)
            OUTPUT INSERTED.Id
            SELECT
                @propertyId,
                @bookingId,
                @customerUserId,
                SYSUTCDATETIME(),
                NULL
            FROM dbo.Property p
            INNER JOIN dbo.[User] u ON u.Id = @customerUserId
            WHERE p.Id = @propertyId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;
        AddNullableGuid(command, "@bookingId", bookingId);
        command.Parameters.Add("@customerUserId", SqlDbType.UniqueIdentifier).Value = customerUserId;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid conversationId)
        {
            throw new InvalidOperationException("Chat conversation creation did not return a row.");
        }

        return await GetConversationByIdAsync(
            connection,
            conversationId,
            cancellationToken)
            ?? throw new InvalidOperationException("Created chat conversation could not be loaded.");
    }

    public async Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (100)
                c.Id,
                c.PropertyId,
                p.[Name] AS PropertyName,
                c.BookingId,
                c.CustomerUserId,
                u.FullName AS CustomerName,
                p.HostId AS HostUserId,
                c.[Status],
                c.LastMessageAt,
                c.CustomerLastReadAt,
                c.HostLastReadAt,
                c.CreatedAt,
                c.UpdatedAt
            FROM dbo.ChatConversation c
            INNER JOIN dbo.Property p ON p.Id = c.PropertyId
            INNER JOIN dbo.[User] u ON u.Id = c.CustomerUserId
            WHERE @isAdmin = 1
               OR c.CustomerUserId = @userId
               OR p.HostId = @userId
            ORDER BY c.UpdatedAt DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@isAdmin", SqlDbType.Bit).Value = isAdmin;

        return await ReadConversationsAsync(command, cancellationToken);
    }

    public async Task<ChatConversationAccess?> GetConversationAccessAsync(
        Guid conversationId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                c.Id,
                c.PropertyId,
                p.[Name] AS PropertyName,
                c.BookingId,
                c.CustomerUserId,
                u.FullName AS CustomerName,
                p.HostId AS HostUserId,
                c.[Status],
                c.LastMessageAt,
                c.CustomerLastReadAt,
                c.HostLastReadAt,
                c.CreatedAt,
                c.UpdatedAt
            FROM dbo.ChatConversation c
            INNER JOIN dbo.Property p ON p.Id = c.PropertyId
            INNER JOIN dbo.[User] u ON u.Id = c.CustomerUserId
            WHERE c.Id = @conversationId
              AND (
                  @isAdmin = 1
                  OR c.CustomerUserId = @userId
                  OR p.HostId = @userId
              );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@conversationId", SqlDbType.UniqueIdentifier).Value = conversationId;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@isAdmin", SqlDbType.Bit).Value = isAdmin;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var conversation = ReadConversation(reader);
        var isCustomer = conversation.CustomerUserId == userId;
        var isHost = conversation.HostUserId == userId;
        return new ChatConversationAccess(conversation, isCustomer, isHost, isAdmin);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        Guid conversationId,
        DateTime? before,
        int take,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT *
            FROM (
                SELECT TOP (@take)
                    m.Id,
                    m.ConversationId,
                    m.SenderUserId,
                    u.FullName AS SenderName,
                    m.Body,
                    m.CreatedAt
                FROM dbo.ChatMessage m
                INNER JOIN dbo.[User] u ON u.Id = m.SenderUserId
                WHERE m.ConversationId = @conversationId
                  AND (@before IS NULL OR m.CreatedAt < @before)
                ORDER BY m.CreatedAt DESC
            ) latest
            ORDER BY CreatedAt ASC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@conversationId", SqlDbType.UniqueIdentifier).Value = conversationId;
        command.Parameters.Add("@take", SqlDbType.Int).Value = take;
        AddNullableDateTime(command, "@before", before);

        var messages = new List<ChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadMessage(reader));
        }

        return messages;
    }

    public async Task<ChatMessage> AddMessageAsync(
        Guid conversationId,
        Guid senderUserId,
        string body,
        bool senderIsCustomer,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var message = await InsertMessageAsync(
                connection,
                (SqlTransaction)transaction,
                conversationId,
                senderUserId,
                body,
                cancellationToken);

            await TouchConversationAsync(
                connection,
                (SqlTransaction)transaction,
                conversationId,
                message.CreatedAt,
                senderIsCustomer,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return message;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task MarkReadAsync(
        Guid conversationId,
        bool readerIsCustomer,
        CancellationToken cancellationToken)
    {
        var column = readerIsCustomer
            ? "CustomerLastReadAt"
            : "HostLastReadAt";

        var sql = $"""
            UPDATE dbo.ChatConversation
            SET
                {column} = SYSUTCDATETIME(),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @conversationId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@conversationId", SqlDbType.UniqueIdentifier).Value = conversationId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ChatMessage> InsertMessageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid conversationId,
        Guid senderUserId,
        string body,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.ChatMessage (
                ConversationId,
                SenderUserId,
                Body)
            OUTPUT INSERTED.Id
            SELECT
                @conversationId,
                @senderUserId,
                @body
            FROM dbo.[User] u
            WHERE u.Id = @senderUserId;
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.Add("@conversationId", SqlDbType.UniqueIdentifier).Value = conversationId;
        command.Parameters.Add("@senderUserId", SqlDbType.UniqueIdentifier).Value = senderUserId;
        AddText(command, "@body", body, 2000);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid messageId)
        {
            throw new InvalidOperationException("Chat message creation did not return a row.");
        }

        return await GetMessageByIdAsync(
            connection,
            transaction,
            messageId,
            cancellationToken)
            ?? throw new InvalidOperationException("Created chat message could not be loaded.");
    }

    private static async Task<ChatConversation?> GetConversationByIdAsync(
        SqlConnection connection,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                c.Id,
                c.PropertyId,
                p.[Name] AS PropertyName,
                c.BookingId,
                c.CustomerUserId,
                u.FullName AS CustomerName,
                p.HostId AS HostUserId,
                c.[Status],
                c.LastMessageAt,
                c.CustomerLastReadAt,
                c.HostLastReadAt,
                c.CreatedAt,
                c.UpdatedAt
            FROM dbo.ChatConversation c
            INNER JOIN dbo.Property p ON p.Id = c.PropertyId
            INNER JOIN dbo.[User] u ON u.Id = c.CustomerUserId
            WHERE c.Id = @conversationId;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@conversationId", SqlDbType.UniqueIdentifier).Value = conversationId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadConversation(reader)
            : null;
    }

    private static async Task<ChatMessage?> GetMessageByIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                m.Id,
                m.ConversationId,
                m.SenderUserId,
                u.FullName AS SenderName,
                m.Body,
                m.CreatedAt
            FROM dbo.ChatMessage m
            INNER JOIN dbo.[User] u ON u.Id = m.SenderUserId
            WHERE m.Id = @messageId;
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.Add("@messageId", SqlDbType.UniqueIdentifier).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMessage(reader)
            : null;
    }

    private static async Task TouchConversationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid conversationId,
        DateTime messageCreatedAt,
        bool senderIsCustomer,
        CancellationToken cancellationToken)
    {
        var readColumn = senderIsCustomer
            ? "CustomerLastReadAt"
            : "HostLastReadAt";

        var sql = $"""
            UPDATE dbo.ChatConversation
            SET
                LastMessageAt = @messageCreatedAt,
                {readColumn} = @messageCreatedAt,
                UpdatedAt = @messageCreatedAt
            WHERE Id = @conversationId;
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.Add("@conversationId", SqlDbType.UniqueIdentifier).Value = conversationId;
        AddDateTime(command, "@messageCreatedAt", messageCreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ChatConversation>> ReadConversationsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var conversations = new List<ChatConversation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            conversations.Add(ReadConversation(reader));
        }

        return conversations;
    }

    private static ChatConversation ReadConversation(SqlDataReader reader)
    {
        return new ChatConversation(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("PropertyId")),
            reader.GetString(reader.GetOrdinal("PropertyName")),
            ReadNullableGuid(reader, "BookingId"),
            reader.GetGuid(reader.GetOrdinal("CustomerUserId")),
            reader.GetString(reader.GetOrdinal("CustomerName")),
            ReadNullableGuid(reader, "HostUserId"),
            reader.GetString(reader.GetOrdinal("Status")),
            ReadNullableDateTime(reader, "LastMessageAt"),
            ReadNullableDateTime(reader, "CustomerLastReadAt"),
            ReadNullableDateTime(reader, "HostLastReadAt"),
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            reader.GetDateTime(reader.GetOrdinal("UpdatedAt")));
    }

    private static ChatMessage ReadMessage(SqlDataReader reader)
    {
        return new ChatMessage(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("ConversationId")),
            reader.GetGuid(reader.GetOrdinal("SenderUserId")),
            reader.GetString(reader.GetOrdinal("SenderName")),
            reader.GetString(reader.GetOrdinal("Body")),
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")));
    }

    private static Guid? ReadNullableGuid(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static DateTime? ReadNullableDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static void AddNullableGuid(
        SqlCommand command,
        string name,
        Guid? value)
    {
        command.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value =
            value ?? (object)DBNull.Value;
    }

    private static void AddDateTime(
        SqlCommand command,
        string name,
        DateTime value)
    {
        command.Parameters.Add(name, SqlDbType.DateTime2).Value = value;
    }

    private static void AddNullableDateTime(
        SqlCommand command,
        string name,
        DateTime? value)
    {
        command.Parameters.Add(name, SqlDbType.DateTime2).Value =
            value ?? (object)DBNull.Value;
    }

    private static void AddText(
        SqlCommand command,
        string name,
        string value,
        int size = -1)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
        parameter.Value = value;
    }
}
