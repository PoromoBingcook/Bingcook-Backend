using System.Data;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerEmailVerificationRepository
    : IEmailVerificationRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerEmailVerificationRepository(
        SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SavePendingRegistrationOtpAsync(
        string fullName,
        string email,
        string phone,
        string passwordHash,
        string role,
        string otpHash,
        DateTime expiresAt,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.EmailVerificationOtp
                (FullName, Email, Phone, PasswordHash, [Role], OtpHash, ExpiresAt, CreatedAt)
            VALUES
                (@fullName, @email, @phone, @passwordHash, @role, @otpHash, @expiresAt, @createdAt);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddText(command, "@fullName", fullName, 100);
        AddText(command, "@email", email, 100);
        AddText(command, "@phone", phone, 20);
        AddText(command, "@passwordHash", passwordHash);
        AddText(command, "@role", role, 20);
        AddText(command, "@otpHash", otpHash, 128);
        command.Parameters.Add("@expiresAt", SqlDbType.DateTime2).Value = expiresAt;
        command.Parameters.Add("@createdAt", SqlDbType.DateTime2).Value = createdAt;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EmailVerificationOtp?> FindLatestActiveOtpAsync(
        string email,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Id,
                UserId,
                FullName,
                Email,
                Phone,
                PasswordHash,
                [Role],
                OtpHash,
                ExpiresAt,
                ConsumedAt,
                CreatedAt
            FROM dbo.EmailVerificationOtp
            WHERE LOWER(Email) = LOWER(@email)
              AND ConsumedAt IS NULL
              AND ExpiresAt >= @now
            ORDER BY CreatedAt DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddText(command, "@email", email, 100);
        command.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadOtp(reader)
            : null;
    }

    public async Task<EmailVerificationOtp?> FindLatestPendingRegistrationAsync(
        string email,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Id,
                UserId,
                FullName,
                Email,
                Phone,
                PasswordHash,
                [Role],
                OtpHash,
                ExpiresAt,
                ConsumedAt,
                CreatedAt
            FROM dbo.EmailVerificationOtp
            WHERE LOWER(Email) = LOWER(@email)
              AND ConsumedAt IS NULL
            ORDER BY CreatedAt DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddText(command, "@email", email, 100);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadOtp(reader)
            : null;
    }

    public async Task MarkOtpConsumedAsync(
        Guid otpId,
        DateTime consumedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.EmailVerificationOtp
            SET ConsumedAt = @consumedAt
            WHERE Id = @otpId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@otpId", SqlDbType.UniqueIdentifier).Value = otpId;
        command.Parameters.Add("@consumedAt", SqlDbType.DateTime2).Value = consumedAt;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF COL_LENGTH('dbo.[User]', 'EmailVerifiedAt') IS NULL
            BEGIN
                ALTER TABLE dbo.[User] ADD EmailVerifiedAt datetime2 NULL;
            END;

            IF OBJECT_ID('dbo.EmailVerificationOtp', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmailVerificationOtp
                (
                    Id uniqueidentifier NOT NULL
                        CONSTRAINT PK_EmailVerificationOtp PRIMARY KEY
                        DEFAULT NEWID(),
                    UserId uniqueidentifier NULL,
                    FullName nvarchar(100) NOT NULL,
                    Email nvarchar(100) NOT NULL,
                    Phone nvarchar(20) NOT NULL,
                    PasswordHash nvarchar(max) NOT NULL,
                    [Role] nvarchar(20) NOT NULL,
                    OtpHash nvarchar(128) NOT NULL,
                    ExpiresAt datetime2 NOT NULL,
                    ConsumedAt datetime2 NULL,
                    CreatedAt datetime2 NOT NULL
                        CONSTRAINT DF_EmailVerificationOtp_CreatedAt
                        DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX IX_EmailVerificationOtp_Email_CreatedAt
                    ON dbo.EmailVerificationOtp (Email, CreatedAt DESC);
            END;

            IF COL_LENGTH('dbo.EmailVerificationOtp', 'FullName') IS NULL
            BEGIN
                ALTER TABLE dbo.EmailVerificationOtp ADD FullName nvarchar(100) NOT NULL CONSTRAINT DF_EmailVerificationOtp_FullName DEFAULT '';
            END;

            IF COL_LENGTH('dbo.EmailVerificationOtp', 'Phone') IS NULL
            BEGIN
                ALTER TABLE dbo.EmailVerificationOtp ADD Phone nvarchar(20) NOT NULL CONSTRAINT DF_EmailVerificationOtp_Phone DEFAULT '';
            END;

            IF COL_LENGTH('dbo.EmailVerificationOtp', 'PasswordHash') IS NULL
            BEGIN
                ALTER TABLE dbo.EmailVerificationOtp ADD PasswordHash nvarchar(max) NOT NULL CONSTRAINT DF_EmailVerificationOtp_PasswordHash DEFAULT '';
            END;

            IF COL_LENGTH('dbo.EmailVerificationOtp', 'Role') IS NULL
            BEGIN
                ALTER TABLE dbo.EmailVerificationOtp ADD [Role] nvarchar(20) NOT NULL CONSTRAINT DF_EmailVerificationOtp_Role DEFAULT 'Customer';
            END;

            ALTER TABLE dbo.EmailVerificationOtp ALTER COLUMN UserId uniqueidentifier NULL;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static EmailVerificationOtp ReadOtp(SqlDataReader reader)
    {
        return new EmailVerificationOtp(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.IsDBNull(reader.GetOrdinal("UserId"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("UserId")),
            reader.GetString(reader.GetOrdinal("FullName")),
            reader.GetString(reader.GetOrdinal("Email")),
            reader.GetString(reader.GetOrdinal("Phone")),
            reader.GetString(reader.GetOrdinal("PasswordHash")),
            reader.GetString(reader.GetOrdinal("Role")),
            reader.GetString(reader.GetOrdinal("OtpHash")),
            reader.GetDateTime(reader.GetOrdinal("ExpiresAt")),
            reader.IsDBNull(reader.GetOrdinal("ConsumedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("ConsumedAt")),
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
