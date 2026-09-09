using System;

namespace YAGOT_2._0.Services;

/// <summary>
/// تتبع حالة جاهزية وترحيل قواعد البيانات بشكل آمن بين الخيوط (Thread-safe).
/// </summary>
public sealed class MigrationStateTracker
{
    private readonly object _lock = new();

    public bool IsStoreConnected { get; private set; }
    public bool IsUsersConnected { get; private set; }
    public bool IsStoreMigrated { get; private set; }
    public bool IsUsersMigrated { get; private set; }
    public int PendingStoreMigrationsCount { get; private set; }
    public int PendingUsersMigrationsCount { get; private set; }
    public bool IsUsersSchemaCompatible { get; private set; } = true;
    public string? LastErrorMessage { get; private set; }
    public DateTime? LastCheckedAtUtc { get; private set; }
    public bool IsMigrationInProgress { get; private set; }

    public bool IsAllSynchronized
    {
        get
        {
            lock (_lock)
            {
                return IsStoreConnected &&
                       IsUsersConnected &&
                       PendingStoreMigrationsCount == 0 &&
                       PendingUsersMigrationsCount == 0 &&
                       IsUsersSchemaCompatible;
            }
        }
    }

    public void RecordReadiness(
        bool storeConnected,
        bool usersConnected,
        int pendingStoreCount,
        int pendingUsersCount,
        bool usersSchemaCompatible,
        string? errorMessage)
    {
        lock (_lock)
        {
            IsStoreConnected = storeConnected;
            IsUsersConnected = usersConnected;
            PendingStoreMigrationsCount = pendingStoreCount;
            PendingUsersMigrationsCount = pendingUsersCount;
            IsStoreMigrated = pendingStoreCount == 0;
            IsUsersMigrated = pendingUsersCount == 0;
            IsUsersSchemaCompatible = usersSchemaCompatible;
            LastErrorMessage = errorMessage;
            LastCheckedAtUtc = DateTime.UtcNow;
        }
    }

    public void SetMigrationInProgress(bool inProgress)
    {
        lock (_lock)
        {
            IsMigrationInProgress = inProgress;
        }
    }

    public MigrationStatusSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new MigrationStatusSnapshot(
                IsStoreConnected,
                IsUsersConnected,
                IsStoreMigrated,
                IsUsersMigrated,
                PendingStoreMigrationsCount,
                PendingUsersMigrationsCount,
                IsUsersSchemaCompatible,
                LastErrorMessage,
                LastCheckedAtUtc,
                IsMigrationInProgress,
                IsAllSynchronized
            );
        }
    }
}

public sealed record MigrationStatusSnapshot(
    bool IsStoreConnected,
    bool IsUsersConnected,
    bool IsStoreMigrated,
    bool IsUsersMigrated,
    int PendingStoreMigrationsCount,
    int PendingUsersMigrationsCount,
    bool IsUsersSchemaCompatible,
    string? LastErrorMessage,
    DateTime? LastCheckedAtUtc,
    bool IsMigrationInProgress,
    bool IsAllSynchronized
);
