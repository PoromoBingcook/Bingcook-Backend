using BingCook.Api.Models;
using Npgsql;

namespace BingCook.Api.Data;

public sealed class PostgresUserRepository : IUserRepository, IEditableUserRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresUserRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> EmailOrPhoneExistsAsync(
        string email,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM "User"
                WHERE lower(email) = lower(@email)
                   OR phone = @phone
            );
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("phone", phone);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task<UserAccount> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO "User" (fullname, email, phone, password, role)
            VALUES (@fullName, @email, @phone, @passwordHash, @role::user_role)
            RETURNING
                id,
                fullname,
                email,
                phone,
                password,
                role,
                createdat;
            """;

        await using var dbCommand = _dataSource.CreateCommand(sql);
        dbCommand.Parameters.AddWithValue("fullName", command.FullName);
        dbCommand.Parameters.AddWithValue("email", command.Email);
        dbCommand.Parameters.AddWithValue("phone", command.Phone);
        dbCommand.Parameters.AddWithValue("passwordHash", command.PasswordHash);
        dbCommand.Parameters.AddWithValue("role", command.Role);

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
            SELECT
                id,
                fullname,
                email,
                phone,
                password,
                role,
                createdat
            FROM "User"
            WHERE lower(email) = lower(@identity)
               OR phone = @identity
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("identity", identity);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadUser(reader);
    }

    public async Task<UserAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id,
                fullname,
                email,
                phone,
                password,
                role,
                createdat
            FROM "User"
            WHERE lower(email) = lower(@email)
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("email", email);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadUser(reader);
    }

    public async Task<bool> PhoneExistsForOtherUserAsync(
        Guid userId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM "User"
                WHERE id <> @userId AND phone = @phone
            );
            """;
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("phone", phone);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<UserAccount?> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE "User"
            SET fullname = @fullName, phone = @phone
            WHERE id = @userId
            RETURNING id, fullname, email, phone, password, role, createdat;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("fullName", fullName);
        command.Parameters.AddWithValue("phone", phone is null ? DBNull.Value : phone);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }

    private static UserAccount ReadUser(NpgsqlDataReader reader)
    {
        return new UserAccount(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("fullname")),
            reader.IsDBNull(reader.GetOrdinal("email"))
                ? null
                : reader.GetString(reader.GetOrdinal("email")),
            reader.IsDBNull(reader.GetOrdinal("phone"))
                ? null
                : reader.GetString(reader.GetOrdinal("phone")),
            reader.GetString(reader.GetOrdinal("password")),
            reader.GetString(reader.GetOrdinal("role")),
            reader.GetDateTime(reader.GetOrdinal("createdat")));
    }
}
