using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YAGOT_2._0.Data;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Services;

/// <summary>
/// منسق ترقية وقراءة جاهزية قواعد البيانات.
/// يوفر فحوصات جاهزية للقراءة فقط عند الإقلاع، وقفل تزامن موزع (PostgreSQL Advisory Lock)
/// عند تنفيذ الترقيات لضمان عدم حدوث سباق بين النسخ المتعددة أو ترقيات غير منضبطة.
/// </summary>
public sealed class DatabaseMigrationCoordinator
{
    // مفتاح القفل الموزع الفريد لمنظومة ياقوت (YAGOTMIG)
    public const long YagotMigrationAdvisoryLockKey = 0x5941474F544D4947L;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MigrationStateTracker _tracker;
    private readonly ILogger<DatabaseMigrationCoordinator> _logger;

    public DatabaseMigrationCoordinator(
        IServiceScopeFactory scopeFactory,
        MigrationStateTracker tracker,
        ILogger<DatabaseMigrationCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>
    /// فحص الجاهزية للقراءة فقط أثناء إقلاع التطبيق (Read-Only Readiness Check).
    /// لا ينفذ أي عمليات DDL أو تعديل في قواعد البيانات إطلاقاً.
    /// </summary>
    public static async Task<(bool IsReady, string? Reason)> VerifyStartupReadinessAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<DatabaseMigrationCoordinator>();

        logger.LogInformation("DatabaseMigrationCoordinator: جاري فحص الجاهزية للقراءة فقط (Read-Only Readiness Check)...");
        var result = await coordinator.VerifyReadinessAsync(cancellationToken);

        if (!result.IsReady)
        {
            logger.LogError("DatabaseMigrationCoordinator: فشل فحص الجاهزية: {Reason}", result.Reason);
        }
        else
        {
            logger.LogInformation("DatabaseMigrationCoordinator: فحص الجاهزية مكتمل بنجاح تام، جميع القواعد متصلة ومتطابقة.");
        }

        return result;
    }

    /// <summary>
    /// فحص حالة الاتصال والترقيات المعلقة وتوافق المخطط دون أي تعديل هيكلي.
    /// </summary>
    public async Task<(bool IsReady, string? Reason)> VerifyReadinessAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var storeDb = scope.ServiceProvider.GetRequiredService<NeondbContext>();
        var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        bool storeConnected = false;
        bool usersConnected = false;
        var pendingStore = new List<string>();
        var pendingUsers = new List<string>();
        UsersSchemaValidationResult usersSchemaResult = UsersSchemaValidationResult.Success();
        string? failureReason = null;

        try
        {
            storeConnected = await storeDb.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DatabaseMigrationCoordinator: تعذر الاتصال بقاعدة بيانات المتجر.");
        }

        try
        {
            usersConnected = await usersDb.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DatabaseMigrationCoordinator: تعذر الاتصال بقاعدة بيانات المستخدمين.");
        }

        if (!storeConnected || !usersConnected)
        {
            failureReason = $"تعذر الاتصال بالخوادم: المتجر ({(storeConnected ? "متصل" : "غير متصل")}) - المستخدمين ({(usersConnected ? "متصل" : "غير متصل")})";
            _tracker.RecordReadiness(storeConnected, usersConnected, -1, -1, false, failureReason);
            return (false, failureReason);
        }

        try
        {
            pendingStore = (await storeDb.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DatabaseMigrationCoordinator: فشل قراءة الترقيات المعلقة للمتجر.");
            failureReason = "فشل قراءة حالة ترقيات المتجر: " + ex.Message;
        }

        try
        {
            pendingUsers = (await usersDb.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            usersSchemaResult = await UsersSchemaValidator.ValidateSchemaAsync(usersDb, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DatabaseMigrationCoordinator: فشل فحص مخطط المستخدمين.");
            failureReason = failureReason != null ? failureReason + " | " + ex.Message : ex.Message;
        }

        bool isReady = pendingStore.Count == 0 && pendingUsers.Count == 0 && usersSchemaResult.IsValid && failureReason == null;

        if (!isReady && failureReason == null)
        {
            var reasons = new List<string>();
            if (pendingStore.Count > 0)
                reasons.Add($"توجد {pendingStore.Count} ترقيات معلقة للمتجر ({string.Join(", ", pendingStore)})");
            if (pendingUsers.Count > 0)
                reasons.Add($"توجد {pendingUsers.Count} ترقيات معلقة للمستخدمين ({string.Join(", ", pendingUsers)})");
            if (!usersSchemaResult.IsValid)
                reasons.Add($"مخطط جدول المستخدمين غير متوافق: {usersSchemaResult.FailureReason}");

            failureReason = string.Join(" | ", reasons);
        }

        _tracker.RecordReadiness(
            storeConnected,
            usersConnected,
            pendingStore.Count,
            pendingUsers.Count,
            usersSchemaResult.IsValid,
            failureReason);

        return (isReady, failureReason);
    }

    /// <summary>
    /// تنفيذ الترقية المنضبطة تحت حماية القفل الموزع (PostgreSQL Advisory Lock).
    /// يُستدعى فقط في مراحل النشر المعزولة أو المزامنة الإدارية المصرح بها.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ExecuteControlledMigrationAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var storeDb = scope.ServiceProvider.GetRequiredService<NeondbContext>();
        var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var connection = storeDb.Database.GetDbConnection();
        bool closeOnExit = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            closeOnExit = true;
        }

        bool lockAcquired = false;

        try
        {
            // 1. محاولة حيازة القفل الموزع لمنع أي تضارب بين النسخ المتعددة
            lockAcquired = await TryAcquireAdvisoryLockAsync(connection, cancellationToken);
            if (!lockAcquired)
            {
                _logger.LogWarning("DatabaseMigrationCoordinator: تم رفض الترقية لأن القفل الموزع محجوز بواسطة خادم آخر.");
                return (false, "عملية ترقية قواعد البيانات قيد التنفيذ حالياً بواسطة خادم أو مشغل آخر.");
            }

            _tracker.SetMigrationInProgress(true);
            _logger.LogInformation("DatabaseMigrationCoordinator: تم الحصول على القفل الموزع. بدء تنفيذ الترقية المنضبطة...");

            // 2. ترقية قاعدة بيانات المتجر
            _logger.LogInformation("DatabaseMigrationCoordinator: ترقية قاعدة بيانات المتجر...");
            await storeDb.Database.MigrateAsync(cancellationToken);

            // 3. فحص وتهيئة قاعدة بيانات المستخدمين
            var schemaCheck = await UsersSchemaValidator.ValidateSchemaAsync(usersDb, cancellationToken);
            if (!schemaCheck.TableExists)
            {
                _logger.LogInformation("DatabaseMigrationCoordinator: تهيئة جدول المستخدمين للبيئة الجديدة...");
                await UsersSchemaValidator.EnsureTableCreatedForFreshDatabaseAsync(usersDb, cancellationToken);
            }
            else if (!schemaCheck.IsValid)
            {
                throw new InvalidOperationException("مخطط جدول المستخدمين غير متوافق: " + schemaCheck.FailureReason);
            }

            // 4. ترقية قاعدة بيانات المستخدمين
            _logger.LogInformation("DatabaseMigrationCoordinator: ترقية قاعدة بيانات المستخدمين...");
            await usersDb.Database.MigrateAsync(cancellationToken);

            // 5. إعادة فحص الجاهزية وتحديث المتتبع
            await VerifyReadinessAsync(cancellationToken);

            _logger.LogInformation("DatabaseMigrationCoordinator: اكتملت الترقية المنضبطة بنجاح تام.");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DatabaseMigrationCoordinator: فشلت عملية الترقية المنضبطة.");
            await VerifyReadinessAsync(cancellationToken);
            return (false, ex.Message);
        }
        finally
        {
            if (lockAcquired)
            {
                await ReleaseAdvisoryLockAsync(connection, cancellationToken);
                _logger.LogInformation("DatabaseMigrationCoordinator: تم تحرير القفل الموزع.");
            }

            _tracker.SetMigrationInProgress(false);

            if (closeOnExit && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(@key);";
        var param = cmd.CreateParameter();
        param.ParameterName = "@key";
        param.Value = YagotMigrationAdvisoryLockKey;
        cmd.Parameters.Add(param);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is bool b && b;
    }

    private static async Task ReleaseAdvisoryLockAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pg_advisory_unlock(@key);";
            var param = cmd.CreateParameter();
            param.ParameterName = "@key";
            param.Value = YagotMigrationAdvisoryLockKey;
            cmd.Parameters.Add(param);

            await cmd.ExecuteScalarAsync(cancellationToken);
        }
        catch
        {
            // تجاهل أخطاء تحرير القفل عند إغلاق الاتصال
        }
    }
}
