using System.Data;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerUserRepository : IUserRepository, IEditableUserRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerUserRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> EmailOrPhoneExistsAsync(
        string email,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.[User]
                WHERE LOWER(Email) = LOWER(@email)
                   OR Phone = @phone
            ) THEN 1 ELSE 0 END AS bit);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddText(command, "@email", email, 100);
        AddText(command, "@phone", phone, 20);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    public async Task<UserAccount> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.[User] (FullName, Email, Phone, [Password], [Role])
            OUTPUT
                INSERTED.Id,
                INSERTED.FullName,
                INSERTED.Email,
                INSERTED.Phone,
                INSERTED.[Password],
                INSERTED.[Role],
                INSERTED.CreatedAt
            VALUES (@fullName, @email, @phone, @passwordHash, @role);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText = sql;
        AddText(dbCommand, "@fullName", command.FullName, 100);
        AddText(dbCommand, "@email", command.Email, 100);
        AddText(dbCommand, "@phone", command.Phone, 20);
        AddText(dbCommand, "@passwordHash", command.PasswordHash);
        AddText(dbCommand, "@role", command.Role, 20);

        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("User creation did not return a row.");
        }

        return ReadUser(reader);
    }

    public async Task<UserAccount?> FindByIdentityAsync(
        string identity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Id,
                FullName,
                Email,
                Phone,
                [Password],
                [Role],
                CreatedAt
            FROM dbo.[User]
            WHERE LOWER(Email) = LOWER(@identity)
               OR Phone = @identity;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddText(command, "@identity", identity, 100);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadUser(reader)
            : null;
    }

    public async Task<UserAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Id,
                FullName,
                Email,
                Phone,
                [Password],
                [Role],
                CreatedAt
            FROM dbo.[User]
            WHERE LOWER(Email) = LOWER(@email);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddText(command, "@email", email, 100);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadUser(reader)
            : null;
    }

    public async Task<bool> PhoneExistsForOtherUserAsync(
        Guid userId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1 FROM dbo.[User]
                WHERE Id <> @userId AND Phone = @phone
            ) THEN 1 ELSE 0 END AS bit);
            """;
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        AddText(command, "@phone", phone, 20);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<UserAccount?> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.[User]
            SET FullName = @fullName, Phone = @phone
            OUTPUT INSERTED.Id, INSERTED.FullName, INSERTED.Email, INSERTED.Phone,
                   INSERTED.[Password], INSERTED.[Role], INSERTED.CreatedAt
            WHERE Id = @userId;
            """;
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        AddText(command, "@fullName", fullName, 100);
        var phoneParameter = command.Parameters.Add("@phone", SqlDbType.NVarChar, 20);
        phoneParameter.Value = phone is null ? DBNull.Value : phone;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }

    private static UserAccount ReadUser(SqlDataReader reader)
    {
        return new UserAccount(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetString(reader.GetOrdinal("FullName")),
            reader.IsDBNull(reader.GetOrdinal("Email"))
                ? null
                : reader.GetString(reader.GetOrdinal("Email")),
            reader.IsDBNull(reader.GetOrdinal("Phone"))
                ? null
                : reader.GetString(reader.GetOrdinal("Phone")),
            reader.GetString(reader.GetOrdinal("Password")),
            reader.GetString(reader.GetOrdinal("Role")),
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")));
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
