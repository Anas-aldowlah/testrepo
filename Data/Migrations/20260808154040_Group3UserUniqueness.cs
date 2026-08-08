using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Data.Migrations
{
    /// <inheritdoc />
    public partial class Group3UserUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This context maps an existing account database. Normalize and repair
            // duplicate identifiers before adding its first managed constraints.
            migrationBuilder.Sql(
                """
                UPDATE users AS u
                SET email = 'customer_dup_' || u.id || '@yaqoot.local'
                FROM (
                    SELECT id, ROW_NUMBER() OVER (
                        PARTITION BY LOWER(BTRIM(email)) ORDER BY id
                    ) AS duplicate_rank
                    FROM users
                    WHERE email IS NOT NULL AND BTRIM(email) <> ''
                ) AS ranked
                WHERE u.id = ranked.id AND ranked.duplicate_rank > 1;

                UPDATE users SET email = LOWER(BTRIM(email))
                WHERE email IS NOT NULL AND BTRIM(email) <> '';

                UPDATE users AS u
                SET phone = 'phone_dup_' || u.id
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY phone ORDER BY id) AS duplicate_rank
                    FROM users
                ) AS ranked
                WHERE u.id = ranked.id AND ranked.duplicate_rank > 1;

                SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM users;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_phone",
                table: "users",
                column: "phone",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ux_users_email", table: "users");
            migrationBuilder.DropIndex(name: "ux_users_phone", table: "users");
        }
    }
}
