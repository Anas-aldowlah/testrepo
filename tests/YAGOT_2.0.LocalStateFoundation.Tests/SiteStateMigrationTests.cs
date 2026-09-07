using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateMigrationTests
{
    [Fact]
    public async Task Migration_AppliesExactFoundationWithoutSeedOrPublicChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            PostgreSqlTestDatabase.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{PostgreSqlTestDatabase.ConnectionVariable} must identify an approved disposable local PostgreSQL database.");
        }

        var publicBefore = await PostgreSqlTestDatabase
            .ReadPublicTableNamesAsync(connectionString);

        await using (var database = await PostgreSqlTestDatabase.CreateAsync(applyMigrations: true))
        {
            var tables = await database.ReadTableNamesAsync();
            Assert.Contains("local_site_state_snapshots", tables);
            Assert.Contains("site_state_event_receipts", tables);
            Assert.Contains("__EFMigrationsHistory", tables);

            await using var context = database.CreateContext();
            Assert.Empty(await context.LocalSiteStateSnapshots.ToListAsync());
            Assert.Empty(await context.SiteStateEventReceipts.ToListAsync());

            await AssertExactColumnsAsync(database.ConnectionString, database.SchemaName);
            await AssertExactConstraintsAndIndexAsync(
                database.ConnectionString,
                database.SchemaName);
        }

        var publicAfter = await PostgreSqlTestDatabase
            .ReadPublicTableNamesAsync(connectionString);
        Assert.Equal(publicBefore, publicAfter);
    }

    private static async Task AssertExactColumnsAsync(
        string connectionString,
        string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name, column_name, data_type,
                   COALESCE(character_maximum_length, -1), is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = @schema
              AND table_name IN ('local_site_state_snapshots', 'site_state_event_receipts')
            ORDER BY table_name, ordinal_position;
            """;
        command.Parameters.AddWithValue("schema", schemaName);
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(string.Join('|',
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? "NULL" : reader.GetString(5)));
        }

        Assert.Equal(
        [
            "local_site_state_snapshots|siteid|integer|-1|NO|NULL",
            "local_site_state_snapshots|contractversion|integer|-1|NO|NULL",
            "local_site_state_snapshots|mode|character varying|20|NO|NULL",
            "local_site_state_snapshots|revision|bigint|-1|NO|NULL",
            "local_site_state_snapshots|effectiveatutc|timestamp with time zone|-1|NO|NULL",
            "local_site_state_snapshots|expiresatutc|timestamp with time zone|-1|NO|NULL",
            "local_site_state_snapshots|sitename|character varying|200|NO|NULL",
            "local_site_state_snapshots|siteurl|text|-1|NO|NULL",
            "local_site_state_snapshots|startdate|date|-1|NO|NULL",
            "local_site_state_snapshots|originaldurationdays|integer|-1|NO|NULL",
            "site_state_event_receipts|deliveryid|uuid|-1|NO|NULL",
            "site_state_event_receipts|siteid|integer|-1|NO|NULL",
            "site_state_event_receipts|revision|bigint|-1|NO|NULL",
            "site_state_event_receipts|payloadsha256|bytea|-1|NO|NULL",
            "site_state_event_receipts|decision|character varying|20|NO|NULL",
            "site_state_event_receipts|recordedatutc|timestamp with time zone|-1|NO|NULL"
        ], columns);
    }

    private static async Task AssertExactConstraintsAndIndexAsync(
        string connectionString,
        string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var constraintCommand = connection.CreateCommand();
        constraintCommand.CommandText = """
            SELECT conname
            FROM pg_constraint c
            JOIN pg_namespace n ON n.oid = c.connamespace
            WHERE n.nspname = @schema
              AND c.contype IN ('p', 'c')
              AND conrelid IN (
                  (@schema || '.local_site_state_snapshots')::regclass,
                  (@schema || '.site_state_event_receipts')::regclass)
            ORDER BY conname;
            """;
        constraintCommand.Parameters.AddWithValue("schema", schemaName);
        await using var constraintReader = await constraintCommand.ExecuteReaderAsync();
        var constraints = new List<string>();
        while (await constraintReader.ReadAsync())
        {
            constraints.Add(constraintReader.GetString(0));
        }

        Assert.Equal(
        [
            "ck_local_site_state_contract_version",
            "ck_local_site_state_duration",
            "ck_local_site_state_mode",
            "ck_local_site_state_revision",
            "ck_local_site_state_site_id",
            "ck_site_state_event_receipts_decision",
            "ck_site_state_event_receipts_hash_length",
            "ck_site_state_event_receipts_revision",
            "ck_site_state_event_receipts_site_id",
            "local_site_state_snapshots_pkey",
            "site_state_event_receipts_pkey"
        ], constraints);

        await constraintReader.DisposeAsync();
        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = @schema
              AND tablename = 'site_state_event_receipts'
            ORDER BY indexname;
            """;
        indexCommand.Parameters.AddWithValue("schema", schemaName);
        await using var indexReader = await indexCommand.ExecuteReaderAsync();
        var indexes = new List<string>();
        while (await indexReader.ReadAsync())
        {
            indexes.Add(indexReader.GetString(0));
        }

        Assert.Equal(
        [
            "ix_site_state_event_receipts_site_revision",
            "site_state_event_receipts_pkey"
        ], indexes);
    }
}
