using System.Data;
using Identity.BLL.Authentication;
using Identity.Models.Authentication;
using Microsoft.Data.SqlClient;

namespace Identity.DAL.Repositories;

public sealed class SqlUserRepository(string connectionString) : IUserRepository
{
    private readonly string _connectionString = new SqlConnectionStringBuilder(connectionString)
    {
        ConnectTimeout = 5,
        ConnectRetryCount = 0
    }.ConnectionString;
    private const string SelectUser = """
        SELECT u.UserID, u.Username, u.FullName, u.PasswordHash, r.RoleName,
               u.WarehouseID, u.Status, u.RowVersion
        FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
        """;

    public Task<AuthUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
        FindAsync("u.Username = @value", new SqlParameter("@value", SqlDbType.NVarChar, 100) { Value = username }, cancellationToken);

    public Task<AuthUser?> FindByIdAsync(int id, CancellationToken cancellationToken) =>
        FindAsync("u.UserID = @value", new SqlParameter("@value", SqlDbType.Int) { Value = id }, cancellationToken);

    public async Task<UserDisplay[]> ListDisplayNamesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        // Read only display fields; hashes and contact details never leave Identity service.
        await using var command = new SqlCommand("SELECT UserID, Username, FullName FROM dbo.[User] ORDER BY FullName, UserID", connection) { CommandTimeout = 5 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<UserDisplay>();
        while (await reader.ReadAsync(cancellationToken)) users.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return users.ToArray();
    }

    private async Task<AuthUser?> FindAsync(string predicate, SqlParameter parameter, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectUser + " WHERE " + predicate, connection);
        command.CommandTimeout = 5;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.GetString(6), (byte[])reader[7]);
    }

    public async Task UpdatePasswordHashAsync(AuthUser user, string hash, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            UPDATE dbo.[User] SET PasswordHash = @hash WHERE UserID = @id AND RowVersion = @version
            """, connection);
        command.CommandTimeout = 5;
        command.Parameters.Add("@hash", SqlDbType.NVarChar, 512).Value = hash;
        command.Parameters.Add("@id", SqlDbType.Int).Value = user.UserId;
        command.Parameters.Add("@version", SqlDbType.Timestamp, 8).Value = user.RowVersion;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
