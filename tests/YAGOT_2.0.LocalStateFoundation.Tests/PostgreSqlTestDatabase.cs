using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    public const string ConnectionVariable = "YAGOT01_TEST_DB";
    private const string FoundationMigrationSuffix = "_YAGOT01LocalStateFoundation";
    private const string ReconciliationMigrationSuffix =
        "_YAGOT03SiteStateSyncCheckpoint";

    private readonly string _adminConnectionString;
    private readonly string _schemaName;

    private PostgreSqlTestDatabase(
        string adminConnectionString,
        string schemaName,
        string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _schemaName = schemaName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
    public string SchemaName => _schemaName;

    public static async Task<PostgreSqlTestDatabase> CreateAsync(
        bool applyMigrations = false)
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} must identify an approved disposable local PostgreSQL database.");
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        if (!IsLoopback(adminBuilder.Host))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} must use a loopback PostgreSQL host.");
        }

        var schemaName = $"yagot01_{Guid.NewGuid():N}";
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            SearchPath = schemaName,
            Pooling = false
        };

        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\";";
            await command.ExecuteNonQueryAsync();
        }

        var database = new PostgreSqlTestDatabase(
            adminConnectionString,
            schemaName,
            testBuilder.ConnectionString);

        try
        {
            await using var context = database.CreateContext();
            if (applyMigrations)
            {
                await database.PreparePreFoundationMigrationBaselineAsync(context);
                await context.Database.MigrateAsync();
            }
            else
            {
                await database.CreateFoundationTablesAsync();
            }

            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public NeondbContext CreateContext(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<NeondbContext>()
            .UseNpgsql(ConnectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new NeondbContext(builder.Options);
    }

    public ServiceProvider CreateRetryEnabledServiceProvider(
        TimeProvider timeProvider,
        params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddSingleton(timeProvider);
        services.AddDbContext<NeondbContext>(options =>
        {
            options.UseNpgsql(ConnectionString, npgsqlOptions =>
                npgsqlOptions.EnableRetryOnFailure(
                    5,
                    TimeSpan.FromMilliseconds(200),
                    null));
            if (interceptors.Length > 0)
            {
                options.AddInterceptors(interceptors);
            }
        });
        services.AddLocalSiteRuntimeState();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
    }

    public async Task<IReadOnlyList<string>> ReadTableNamesAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;
        command.Parameters.AddWithValue("schema", SchemaName);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public static async Task<IReadOnlyList<string>> ReadPublicTableNamesAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA \"{_schemaName}\" CASCADE;";
        await command.ExecuteNonQueryAsync();
    }

    private static bool IsLoopback(string? host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    private async Task CreateFoundationTablesAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE local_site_state_snapshots (
                siteid integer NOT NULL,
                contractversion integer NOT NULL,
                mode character varying(20) NOT NULL,
                revision bigint NOT NULL,
                effectiveatutc timestamp with time zone NOT NULL,
                expiresatutc timestamp with time zone NOT NULL,
                sitename character varying(200) NOT NULL,
                siteurl text NOT NULL,
                startdate date NOT NULL,
                originaldurationdays integer NOT NULL,
                CONSTRAINT local_site_state_snapshots_pkey PRIMARY KEY (siteid),
                CONSTRAINT ck_local_site_state_contract_version CHECK (contractversion = 1),
                CONSTRAINT ck_local_site_state_duration CHECK (originaldurationdays > 0),
                CONSTRAINT ck_local_site_state_mode CHECK (mode IN ('Online', 'Development', 'Offline')),
                CONSTRAINT ck_local_site_state_revision CHECK (revision >= 1),
                CONSTRAINT ck_local_site_state_site_id CHECK (siteid = 1)
            );

            CREATE TABLE site_state_event_receipts (
                deliveryid uuid NOT NULL,
                siteid integer NOT NULL,
                revision bigint NOT NULL,
                payloadsha256 bytea NOT NULL,
                decision character varying(20) NOT NULL,
                recordedatutc timestamp with time zone NOT NULL,
                CONSTRAINT site_state_event_receipts_pkey PRIMARY KEY (deliveryid),
                CONSTRAINT ck_site_state_event_receipts_decision CHECK (decision IN ('Applied', 'Equal', 'EqualConflict', 'Stale')),
                CONSTRAINT ck_site_state_event_receipts_hash_length CHECK (octet_length(payloadsha256) = 32),
                CONSTRAINT ck_site_state_event_receipts_revision CHECK (revision >= 1),
                CONSTRAINT ck_site_state_event_receipts_site_id CHECK (siteid = 1)
            );

            CREATE INDEX ix_site_state_event_receipts_site_revision
                ON site_state_event_receipts (siteid, revision);

            CREATE TABLE site_state_sync_checkpoints (
                siteid integer NOT NULL,
                lastattemptatutc timestamp with time zone NOT NULL,
                lastsuccessatutc timestamp with time zone NULL,
                lastobservedremoterevision bigint NULL,
                consecutivefailures integer NOT NULL,
                lastfailurecode character varying(64) NULL,
                updatedatutc timestamp with time zone NOT NULL,
                CONSTRAINT site_state_sync_checkpoints_pkey PRIMARY KEY (siteid),
                CONSTRAINT ck_site_state_sync_checkpoints_failures CHECK (consecutivefailures >= 0),
                CONSTRAINT ck_site_state_sync_checkpoints_remote_revision CHECK (lastobservedremoterevision IS NULL OR lastobservedremoterevision >= 1),
                CONSTRAINT ck_site_state_sync_checkpoints_site_id CHECK (siteid = 1)
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task PreparePreFoundationMigrationBaselineAsync(NeondbContext context)
    {
        var migrations = context.Database.GetMigrations().ToArray();
        var foundationMigration = migrations.SingleOrDefault(
            migration => migration.EndsWith(
                FoundationMigrationSuffix,
                StringComparison.Ordinal));
        var reconciliationMigration = migrations.SingleOrDefault(
            migration => migration.EndsWith(
                ReconciliationMigrationSuffix,
                StringComparison.Ordinal));
        if (foundationMigration is null ||
            reconciliationMigration is null ||
            reconciliationMigration != migrations[^1] ||
            Array.IndexOf(migrations, foundationMigration) >=
            Array.IndexOf(migrations, reconciliationMigration))
        {
            throw new InvalidOperationException(
                "The YAGOT-01 migration must precede the latest YAGOT-03 migration.");
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var createHistory = connection.CreateCommand())
        {
            createHistory.Transaction = transaction;
            createHistory.CommandText = """
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """;
            await createHistory.ExecuteNonQueryAsync();
        }

        foreach (var migration in migrations.TakeWhile(migration =>
                     !string.Equals(
                         migration,
                         foundationMigration,
                         StringComparison.Ordinal)))
        {
            await using var insertHistory = connection.CreateCommand();
            insertHistory.Transaction = transaction;
            insertHistory.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES (@migrationId, '9.0.0');
                """;
            insertHistory.Parameters.AddWithValue("migrationId", migration);
            await insertHistory.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
}
