using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MigrationSchemaTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task UsersMigration_ShouldCreateUsersTableWithUniqueLogin()
    {
        await fixture.ResetUsersDatabaseAsync();

        await using var context = fixture.CreateUsersContext();
        var connection = context.Database.GetDbConnection();

        Assert.Equal(1, await TableCountAsync(connection, "Users"));
        Assert.Equal("NO", await GetColumnValueAsync(connection, "Users", "Login", "is_nullable"));
        Assert.Equal("100", await GetColumnValueAsync(connection, "Users", "Login", "character_maximum_length"));
        Assert.Equal("128", await GetColumnValueAsync(connection, "Users", "PasswordHash", "character_maximum_length"));
        Assert.Equal(1, await IndexCountAsync(connection, "Users", "IX_Users_Login", unique: true));
    }

    [Fact]
    public async Task EventsMigration_ShouldCreateEventsTableWithSeatColumns()
    {
        await fixture.ResetEventsDatabaseAsync();

        await using var context = fixture.CreateEventsContext();
        var connection = context.Database.GetDbConnection();

        Assert.Equal(1, await TableCountAsync(connection, "Events"));
        Assert.Equal("NO", await GetColumnValueAsync(connection, "Events", "Title", "is_nullable"));
        Assert.Equal("200", await GetColumnValueAsync(connection, "Events", "Title", "character_maximum_length"));
        Assert.Equal("NO", await GetColumnValueAsync(connection, "Events", "TotalSeats", "is_nullable"));
        Assert.Equal("NO", await GetColumnValueAsync(connection, "Events", "AvailableSeats", "is_nullable"));
    }

    [Fact]
    public async Task BookingsMigration_ShouldCreateBookingsTableWithoutCrossServiceForeignKeys()
    {
        await fixture.ResetBookingsDatabaseAsync();

        await using var context = fixture.CreateBookingsContext();
        var connection = context.Database.GetDbConnection();

        Assert.Equal(1, await TableCountAsync(connection, "Bookings"));
        Assert.Equal("NO", await GetColumnValueAsync(connection, "Bookings", "EventId", "is_nullable"));
        Assert.Equal("NO", await GetColumnValueAsync(connection, "Bookings", "UserId", "is_nullable"));
        Assert.Equal("32", await GetColumnValueAsync(connection, "Bookings", "Status", "character_maximum_length"));
        Assert.Equal(0, await ForeignKeyCountAsync(connection, "Bookings"));
        Assert.Equal(1, await IndexCountAsync(connection, "Bookings", "IX_Bookings_EventId"));
        Assert.Equal(1, await IndexCountAsync(connection, "Bookings", "IX_Bookings_UserId"));
    }

    private static async Task<long> TableCountAsync(DbConnection connection, string tableName)
    {
        return await ExecuteScalarAsync<long>(
            connection,
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = @tableName;
            """,
            command => AddParameter(command, "tableName", tableName));
    }

    private static async Task<long> IndexCountAsync(
        DbConnection connection,
        string tableName,
        string indexName,
        bool unique = false)
    {
        return await ExecuteScalarAsync<long>(
            connection,
            """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = @tableName
              AND indexname = @indexName
              AND (@unique = FALSE OR indexdef ILIKE '%UNIQUE%');
            """,
            command =>
            {
                AddParameter(command, "tableName", tableName);
                AddParameter(command, "indexName", indexName);
                AddParameter(command, "unique", unique);
            });
    }

    private static async Task<long> ForeignKeyCountAsync(DbConnection connection, string tableName)
    {
        return await ExecuteScalarAsync<long>(
            connection,
            """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE contype = 'f'
              AND conrelid = CAST(@tableName AS regclass);
            """,
            command => AddParameter(command, "tableName", $@"""{tableName}"""));
    }

    private static async Task<string> GetColumnValueAsync(
        DbConnection connection,
        string tableName,
        string columnName,
        string selectedColumn)
    {
        return await ExecuteScalarAsync<string>(
            connection,
            $"""
            SELECT {selectedColumn}::text
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @tableName
              AND column_name = @columnName;
            """,
            command =>
            {
                AddParameter(command, "tableName", tableName);
                AddParameter(command, "columnName", columnName);
            });
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        DbConnection connection,
        string commandText,
        Action<DbCommand>? configureCommand = null)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureCommand?.Invoke(command);

        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        return (T)Convert.ChangeType(result, typeof(T));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
