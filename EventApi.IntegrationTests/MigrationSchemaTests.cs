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
    public async Task MigrateAsync_ShouldCreateEventsBookingsAndUsersTables()
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
              AND table_name IN ('Events', 'Bookings', 'Users');
            """);

        // Assert
        Assert.Equal(3, tableCount);
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
        var usersIdIdentity = await GetColumnValueAsync(connection, "Users", "Id", "is_identity");
        var eventsPrimaryKeyExists = await ConstraintExistsAsync(connection, "Events", "PK_Events", "p");
        var bookingsPrimaryKeyExists = await ConstraintExistsAsync(connection, "Bookings", "PK_Bookings", "p");
        var usersPrimaryKeyExists = await ConstraintExistsAsync(connection, "Users", "PK_Users", "p");

        // Assert
        Assert.Equal("YES", eventsIdIdentity);
        Assert.Equal("YES", bookingsIdIdentity);
        Assert.Equal("YES", usersIdIdentity);
        Assert.True(eventsPrimaryKeyExists);
        Assert.True(bookingsPrimaryKeyExists);
        Assert.True(usersPrimaryKeyExists);
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
    public async Task MigrateAsync_ShouldConfigureBookingsUserForeignKey()
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
            WHERE conname = 'FK_Bookings_Users_UserId'
              AND contype = 'f'
              AND conrelid = '"Bookings"'::regclass
              AND confrelid = '"Users"'::regclass;
            """);

        // Assert
        Assert.Equal(
            """FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE""",
            foreignKeyDefinition);
    }

    [Fact]
    public async Task MigrateAsync_ShouldConfigureUsersLoginUniqueIndex()
    {
        // Arrange
        await using var context = fixture.CreateContext();
        var connection = context.Database.GetDbConnection();

        // Act
        var indexCount = await ExecuteScalarAsync<long>(
            connection,
            """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'Users'
              AND indexname = 'IX_Users_Login'
              AND indexdef ILIKE '%UNIQUE%';
            """);

        // Assert
        Assert.Equal(1, indexCount);
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
        var loginIsNullable = await GetColumnValueAsync(connection, "Users", "Login", "is_nullable");
        var loginMaxLength = await GetColumnValueAsync(connection, "Users", "Login", "character_maximum_length");
        var passwordHashIsNullable = await GetColumnValueAsync(connection, "Users", "PasswordHash", "is_nullable");
        var passwordHashMaxLength = await GetColumnValueAsync(connection, "Users", "PasswordHash", "character_maximum_length");
        var roleIsNullable = await GetColumnValueAsync(connection, "Users", "Role", "is_nullable");
        var roleMaxLength = await GetColumnValueAsync(connection, "Users", "Role", "character_maximum_length");

        // Assert
        Assert.Equal("NO", titleIsNullable);
        Assert.Equal("200", titleMaxLength);
        Assert.Equal("NO", statusIsNullable);
        Assert.Equal("32", statusMaxLength);
        Assert.Equal("YES", processedAtIsNullable);
        Assert.Equal("NO", loginIsNullable);
        Assert.Equal("100", loginMaxLength);
        Assert.Equal("NO", passwordHashIsNullable);
        Assert.Equal("128", passwordHashMaxLength);
        Assert.Equal("NO", roleIsNullable);
        Assert.Equal("32", roleMaxLength);
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
