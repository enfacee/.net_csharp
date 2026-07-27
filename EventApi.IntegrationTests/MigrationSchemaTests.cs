using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MigrationSchemaTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MigrateAsync_ShouldCreateEventsAndBookingsTables()
    {
        // Arrange
        await using var context = fixture.CreateContext();
        var connection = context.Database.GetDbConnection();

        // Act
        var tableCount = await ExecuteScalarAsync<long>(
            connection,
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('Events', 'Bookings');
            """);

        // Assert
        Assert.Equal(2, tableCount);
    }

    [Fact]
    public async Task MigrateAsync_ShouldConfigurePrimaryKeysAndDatabaseGeneratedIds()
    {
        // Arrange
        await using var context = fixture.CreateContext();
        var connection = context.Database.GetDbConnection();

        // Act
        var eventsIdIdentity = await GetColumnValueAsync(connection, "Events", "Id", "is_identity");
        var bookingsIdIdentity = await GetColumnValueAsync(connection, "Bookings", "Id", "is_identity");
        var eventsPrimaryKeyExists = await ConstraintExistsAsync(connection, "Events", "PK_Events", "p");
        var bookingsPrimaryKeyExists = await ConstraintExistsAsync(connection, "Bookings", "PK_Bookings", "p");

        // Assert
        Assert.Equal("YES", eventsIdIdentity);
        Assert.Equal("YES", bookingsIdIdentity);
        Assert.True(eventsPrimaryKeyExists);
        Assert.True(bookingsPrimaryKeyExists);
    }

    [Fact]
    public async Task MigrateAsync_ShouldConfigureBookingsEventForeignKey()
    {
        // Arrange
        await using var context = fixture.CreateContext();
        var connection = context.Database.GetDbConnection();

        // Act
        var foreignKeyDefinition = await ExecuteScalarAsync<string>(
            connection,
            """
            SELECT pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conname = 'FK_Bookings_Events_EventId'
              AND contype = 'f'
              AND conrelid = '"Bookings"'::regclass
              AND confrelid = '"Events"'::regclass;
            """);

        // Assert
        Assert.Equal(
            """FOREIGN KEY ("EventId") REFERENCES "Events"("Id") ON DELETE CASCADE""",
            foreignKeyDefinition);
    }

    [Fact]
    public async Task MigrateAsync_ShouldConfigureRequiredColumnsAndStringLengths()
    {
        // Arrange
        await using var context = fixture.CreateContext();
        var connection = context.Database.GetDbConnection();

        // Act
        var titleIsNullable = await GetColumnValueAsync(connection, "Events", "Title", "is_nullable");
        var titleMaxLength = await GetColumnValueAsync(connection, "Events", "Title", "character_maximum_length");
        var statusIsNullable = await GetColumnValueAsync(connection, "Bookings", "Status", "is_nullable");
        var statusMaxLength = await GetColumnValueAsync(connection, "Bookings", "Status", "character_maximum_length");
        var processedAtIsNullable = await GetColumnValueAsync(connection, "Bookings", "ProcessedAt", "is_nullable");

        // Assert
        Assert.Equal("NO", titleIsNullable);
        Assert.Equal("200", titleMaxLength);
        Assert.Equal("NO", statusIsNullable);
        Assert.Equal("32", statusMaxLength);
        Assert.Equal("YES", processedAtIsNullable);
    }

    private static async Task<bool> ConstraintExistsAsync(
        DbConnection connection,
        string tableName,
        string constraintName,
        string constraintType)
    {
        var count = await ExecuteScalarAsync<long>(
            connection,
            """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE conname = @constraintName
              AND contype = @constraintType
              AND conrelid = CAST(@tableName AS regclass);
            """,
            command =>
            {
                AddParameter(command, "constraintName", constraintName);
                AddParameter(command, "constraintType", constraintType);
                AddParameter(command, "tableName", $@"""{tableName}""");
            });

        return count == 1;
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
