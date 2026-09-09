using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;
using YAGOT_2._0.Data;
using YAGOT_2._0.Data.Migrations;
using YAGOT_2._0.Migrations;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public class DatabaseMigrationRemediationTests
{
    [Fact]
    public void UpdateModels_Anas_Up_DoesNotContainDuplicateFkUserLogs()
    {
        var migration = new UpdateModels_Anas();
        var builder = new MigrationBuilder("Npgsql");

        // Act - invoke protected Up method
        var upMethod = typeof(UpdateModels_Anas).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(upMethod);
        upMethod.Invoke(migration, new object[] { builder });

        // Assert: No AddForeignKeyOperation with name "fk_user_logs"
        var addFkOps = builder.Operations.OfType<AddForeignKeyOperation>()
            .Where(op => op.Name.Equals("fk_user_logs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(addFkOps);

        // Verify preserved operations
        var createIndexOps = builder.Operations.OfType<CreateIndexOperation>()
            .Where(op => op.Name == "ix_sales_invoice_number")
            .ToList();
        Assert.Single(createIndexOps);

        var checkConstraints = builder.Operations.OfType<AddCheckConstraintOperation>().ToList();
        Assert.Contains(checkConstraints, c => c.Name == "CK_SalePayment_Amount_Positive");
        Assert.Contains(checkConstraints, c => c.Name == "CK_SaleItem_Quantity_Positive");
    }

    [Fact]
    public void UpdateModels_Anas_Down_DoesNotDropFkUserLogsNorRecreateSalePaymentsUserid()
    {
        var migration = new UpdateModels_Anas();
        var builder = new MigrationBuilder("Npgsql");

        var downMethod = typeof(UpdateModels_Anas).GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(downMethod);
        downMethod.Invoke(migration, new object[] { builder });

        // Assert: No DropForeignKeyOperation on securitylogs for fk_user_logs
        var dropFkOps = builder.Operations.OfType<DropForeignKeyOperation>()
            .Where(op => op.Name.Equals("fk_user_logs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(dropFkOps);

        // Assert: No AddColumnOperation on sale_payments for Userid
        var addColOps = builder.Operations.OfType<AddColumnOperation>()
            .Where(op => op.Table.Equals("sale_payments", StringComparison.OrdinalIgnoreCase) &&
                         op.Name.Equals("Userid", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(addColOps);
    }

    [Fact]
    public void RestoredStoreMigrations_ExistAndHaveCorrectMetadata()
    {
        // 1. NormalizeEventTimeDefaults
        var timeMigrationType = typeof(NormalizeEventTimeDefaults);
        var timeAttr = timeMigrationType.GetCustomAttribute<MigrationAttribute>();
        var timeDbAttr = timeMigrationType.GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(timeAttr);
        Assert.Equal("20260826061646_NormalizeEventTimeDefaults", timeAttr.Id);
        Assert.NotNull(timeDbAttr);
        Assert.Equal(typeof(NeondbContext), timeDbAttr.ContextType);

        // 2. AddSearchNameToUserSite
        var searchMigrationType = typeof(AddSearchNameToUserSite);
        var searchAttr = searchMigrationType.GetCustomAttribute<MigrationAttribute>();
        var searchDbAttr = searchMigrationType.GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(searchAttr);
        Assert.Equal("20260826170129_AddSearchNameToUserSite", searchAttr.Id);
        Assert.NotNull(searchDbAttr);
        Assert.Equal(typeof(NeondbContext), searchDbAttr.ContextType);

        // 3. Phase7StoreDatabaseOptimizations
        var optMigrationType = typeof(Phase7StoreDatabaseOptimizations);
        var optAttr = optMigrationType.GetCustomAttribute<MigrationAttribute>();
        var optDbAttr = optMigrationType.GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(optAttr);
        Assert.Equal("20260909054954_Phase7StoreDatabaseOptimizations", optAttr.Id);
        Assert.NotNull(optDbAttr);
        Assert.Equal(typeof(NeondbContext), optDbAttr.ContextType);
    }

    [Fact]
    public void RestoredUsersMigration_ExistsAndMatchesProductionLineage()
    {
        var trigramType = typeof(Phase7UsersTrigramIndex);
        var trigramAttr = trigramType.GetCustomAttribute<MigrationAttribute>();
        var trigramDbAttr = trigramType.GetCustomAttribute<DbContextAttribute>();

        Assert.NotNull(trigramAttr);
        Assert.Equal("20260909055139_Phase7UsersTrigramIndex", trigramAttr.Id);
        Assert.NotNull(trigramDbAttr);
        Assert.Equal(typeof(UsersDbContext), trigramDbAttr.ContextType);
    }

    [Fact]
    public void UsersMigrationChain_DoesNotContainUnsafeOlderBaseline()
    {
        var usersMigrations = typeof(UsersDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t))
            .Select(t => t.GetCustomAttribute<MigrationAttribute>())
            .Where(a => a != null)
            .Select(a => a!.Id)
            .Where(id => id.Contains("Users") || id.Contains("Group3User"))
            .OrderBy(id => id)
            .ToList();

        // Must NOT contain 20260808000000_UsersBaseline as a normal pending migration
        Assert.DoesNotContain("20260808000000_UsersBaseline", usersMigrations);

        // Must match exact production sequence: Group3UserUniqueness -> Phase7UsersTrigramIndex
        Assert.Contains("20260808154040_Group3UserUniqueness", usersMigrations);
        Assert.Contains("20260909055139_Phase7UsersTrigramIndex", usersMigrations);
    }

    [Fact]
    public void MigrationStateTracker_TracksReadinessAndFailureAccurately()
    {
        var tracker = new MigrationStateTracker();
        Assert.False(tracker.IsAllSynchronized);

        // Case 1: Pending migrations exist -> not synchronized
        tracker.RecordReadiness(
            storeConnected: true,
            usersConnected: true,
            pendingStoreCount: 2,
            pendingUsersCount: 0,
            usersSchemaCompatible: true,
            errorMessage: "2 pending store migrations");

        var snapshot1 = tracker.GetSnapshot();
        Assert.True(snapshot1.IsStoreConnected);
        Assert.False(snapshot1.IsStoreMigrated);
        Assert.False(snapshot1.IsAllSynchronized);
        Assert.Equal(2, snapshot1.PendingStoreMigrationsCount);

        // Case 2: Schema incompatible -> not synchronized
        tracker.RecordReadiness(
            storeConnected: true,
            usersConnected: true,
            pendingStoreCount: 0,
            pendingUsersCount: 0,
            usersSchemaCompatible: false,
            errorMessage: "Users schema incompatible");

        var snapshot2 = tracker.GetSnapshot();
        Assert.False(snapshot2.IsAllSynchronized);
        Assert.False(snapshot2.IsUsersSchemaCompatible);

        // Case 3: Fully migrated and compatible -> synchronized
        tracker.RecordReadiness(
            storeConnected: true,
            usersConnected: true,
            pendingStoreCount: 0,
            pendingUsersCount: 0,
            usersSchemaCompatible: true,
            errorMessage: null);

        var snapshot3 = tracker.GetSnapshot();
        Assert.True(snapshot3.IsAllSynchronized);
        Assert.True(snapshot3.IsStoreMigrated);
        Assert.True(snapshot3.IsUsersMigrated);
        Assert.Null(snapshot3.LastErrorMessage);
    }

    [Fact]
    public void PostgreSqlAdvisoryLock_KeyIsStableAndApplicationSpecific()
    {
        Assert.Equal(0x5941474F544D4947L, DatabaseMigrationCoordinator.YagotMigrationAdvisoryLockKey);
    }

    [Fact]
    public void UsersModelSnapshot_ContainsTrigramIndex()
    {
        var snapshot = new UsersDbContextModelSnapshot();
        var model = snapshot.Model;

        var userEntity = model.FindEntityType("YAGOT_2._0.Models.UsersDatabase.User");
        Assert.NotNull(userEntity);

        // Check trigram index on Name
        var indexes = userEntity.GetIndexes().ToList();
        var nameIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "Name"));
        Assert.NotNull(nameIndex);
    }
}
