using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Data;

namespace YAGOT_2._0.Services;

public sealed record UsersSchemaValidationResult(bool IsValid, bool TableExists, string? FailureReason)
{
    public static UsersSchemaValidationResult Success() => new(true, true, null);
    public static UsersSchemaValidationResult MissingTable(string reason) => new(false, false, reason);
    public static UsersSchemaValidationResult Incompatible(string reason) => new(false, true, reason);
}

/// <summary>
/// فاحص توافق مخطط جدول المستخدمين public.users دون الاعتماد على IF NOT EXISTS السطحي
/// والتحقق الصارم من الأعمدة وأنواع البيانات والمفاتيح الأساسية لمنع الفشل الصامت.
/// </summary>
public static class UsersSchemaValidator
{
    public static async Task<UsersSchemaValidationResult> ValidateSchemaAsync(
        UsersDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        bool closeOnExit = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            closeOnExit = true;
        }

        try
        {
            // 1. فحص وجود الجدول
            await using var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = @"
                SELECT COUNT(*) 
                FROM information_schema.tables 
                WHERE table_schema = 'public' AND table_name = 'users';";
            var tableCountObj = await tableCmd.ExecuteScalarAsync(cancellationToken);
            long tableCount = Convert.ToInt64(tableCountObj);

            if (tableCount == 0)
            {
                return UsersSchemaValidationResult.MissingTable("جدول المستخدمين public.users غير موجود.");
            }

            // 2. فحص الأعمدة وأنواعها وإلزاميتها
            await using var colCmd = connection.CreateCommand();
            colCmd.CommandText = @"
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'users';";

            var columns = new Dictionary<string, (string DataType, string IsNullable)>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await colCmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    string colName = reader.GetString(0);
                    string dataType = reader.GetString(1);
                    string isNullable = reader.GetString(2);
                    columns[colName] = (dataType, isNullable);
                }
            }

            var requiredColumns = new (string Name, string[] AllowedTypes, bool MustBeNotNull)[]
            {
                ("id", new[] { "integer", "bigint" }, true),
                ("name", new[] { "character varying", "varchar", "text" }, true),
                ("email", new[] { "character varying", "varchar", "text" }, false),
                ("phone", new[] { "character varying", "varchar", "text" }, true),
                ("passwordhash", new[] { "character varying", "varchar", "text" }, true),
                ("createdat", new[] { "timestamp without time zone", "timestamp with time zone", "timestamp" }, false)
            };

            foreach (var (colName, allowedTypes, mustBeNotNull) in requiredColumns)
            {
                if (!columns.TryGetValue(colName, out var colInfo))
                {
                    return UsersSchemaValidationResult.Incompatible($"الحقل الإلزامي '{colName}' مفقود من جدول public.users.");
                }

                bool typeMatch = false;
                foreach (var allowed in allowedTypes)
                {
                    if (colInfo.DataType.IndexOf(allowed, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        typeMatch = true;
                        break;
                    }
                }

                if (!typeMatch)
                {
                    return UsersSchemaValidationResult.Incompatible($"نوع بيانات الحقل '{colName}' هو '{colInfo.DataType}' وهو غير متوافق مع المخطط المتوقع.");
                }

                if (mustBeNotNull && colInfo.IsNullable.Equals("YES", StringComparison.OrdinalIgnoreCase))
                {
                    return UsersSchemaValidationResult.Incompatible($"الحقل '{colName}' يقبل القيم الفارغة (Nullable) بينما يتطلب النظام أن يكون إلزامياً (NOT NULL).");
                }
            }

            // 3. فحص المفتاح الأساسي (Primary Key)
            await using var pkCmd = connection.CreateCommand();
            pkCmd.CommandText = @"
                SELECT COUNT(*) 
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                  ON tc.constraint_name = kcu.constraint_name 
                 AND tc.table_schema = kcu.table_schema
                WHERE tc.table_schema = 'public' 
                  AND tc.table_name = 'users' 
                  AND tc.constraint_type = 'PRIMARY KEY' 
                  AND kcu.column_name = 'id';";

            var pkCountObj = await pkCmd.ExecuteScalarAsync(cancellationToken);
            long pkCount = Convert.ToInt64(pkCountObj);

            if (pkCount == 0)
            {
                return UsersSchemaValidationResult.Incompatible("المفتاح الأساسي (Primary Key) على الحقل 'id' مفقود من جدول public.users.");
            }

            return UsersSchemaValidationResult.Success();
        }
        finally
        {
            if (closeOnExit && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// مخصص لتهيئة البيئات الجديدة الفارغة (Fresh/Integration Tests) دون إقحام EF Migrations قديمة في بيئة الإنتاج.
    /// </summary>
    public static async Task EnsureTableCreatedForFreshDatabaseAsync(
        UsersDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        const string createSql = @"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.tables 
                    WHERE table_schema = 'public' AND table_name = 'users'
                ) THEN
                    CREATE TABLE public.users (
                        id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        name character varying(255) NOT NULL,
                        email character varying(260) NULL,
                        phone text NOT NULL,
                        passwordhash text NOT NULL,
                        createdat timestamp without time zone NULL DEFAULT CURRENT_TIMESTAMP
                    );
                END IF;
            END $$;";

        await dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }
}
