using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class Group3StructuralIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean legacy data before making the constraints enforceable.
            migrationBuilder.Sql(
                """
                UPDATE users AS u
                SET email = 'customer_dup_' || u.id || '@yaqoot.local'
                FROM (
                    SELECT id, ROW_NUMBER() OVER (
                        PARTITION BY LOWER(BTRIM(email))
                        ORDER BY CASE WHEN LOWER(role) IN ('admin', 'developer') THEN 0 ELSE 1 END, id
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

                DELETE FROM "UserSite" WHERE "UserID" IS NULL;

                DELETE FROM "UserSite" AS duplicate
                USING "UserSite" AS keeper
                WHERE duplicate."UserID" = keeper."UserID" AND duplicate.id > keeper.id;

                INSERT INTO "UserSite" ("UserID", "Role")
                SELECT required.userid, 'Customer'
                FROM (
                    SELECT userid FROM carts
                    UNION SELECT userid FROM orders
                    UNION SELECT userid FROM securitylogs WHERE userid IS NOT NULL
                ) AS required
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserSite" AS existing WHERE existing."UserID" = required.userid
                );

                WITH ranked_carts AS (
                    SELECT id, MIN(id) OVER (PARTITION BY userid) AS keeper_id
                    FROM carts
                )
                UPDATE cartitems AS item
                SET cartid = ranked.keeper_id
                FROM ranked_carts AS ranked
                WHERE item.cartid = ranked.id AND ranked.id <> ranked.keeper_id;

                DELETE FROM carts AS duplicate
                USING carts AS keeper
                WHERE duplicate.userid = keeper.userid AND duplicate.id > keeper.id;

                WITH duplicate_items AS (
                    SELECT id,
                           MIN(id) OVER (PARTITION BY cartid, productid) AS keeper_id,
                           SUM(quantity) OVER (PARTITION BY cartid, productid) AS total_quantity
                    FROM cartitems
                )
                UPDATE cartitems AS item
                SET quantity = duplicate_items.total_quantity
                FROM duplicate_items
                WHERE item.id = duplicate_items.keeper_id;

                WITH duplicate_items AS (
                    SELECT id, MIN(id) OVER (PARTITION BY cartid, productid) AS keeper_id
                    FROM cartitems
                )
                DELETE FROM cartitems AS item
                USING duplicate_items
                WHERE item.id = duplicate_items.id AND duplicate_items.id <> duplicate_items.keeper_id;

                SELECT setval(pg_get_serial_sequence('carts', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM carts;
                SELECT setval(pg_get_serial_sequence('cartitems', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM cartitems;
                SELECT setval(pg_get_serial_sequence('orders', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM orders;
                SELECT setval(pg_get_serial_sequence('orderitems', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM orderitems;
                SELECT setval(pg_get_serial_sequence('products', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM products;
                SELECT setval(pg_get_serial_sequence('categories', 'id'), COALESCE(MAX(id), 0) + 1, false) FROM categories;
                """);

            // Some environments materialized the sync-era schema without recording
            // that migration, while others contain its unused shadow relationship.
            migrationBuilder.Sql(
                """
                ALTER TABLE carts DROP CONSTRAINT IF EXISTS "FK_carts_UserSite_UserSiteId";
                ALTER TABLE orders DROP CONSTRAINT IF EXISTS "FK_orders_UserSite_UserSiteId";
                ALTER TABLE securitylogs DROP CONSTRAINT IF EXISTS "FK_securitylogs_UserSite_UserSiteId";
                DROP INDEX IF EXISTS "IX_carts_UserSiteId";
                DROP INDEX IF EXISTS "IX_orders_UserSiteId";
                DROP INDEX IF EXISTS "IX_securitylogs_UserSiteId";
                ALTER TABLE carts DROP COLUMN IF EXISTS "UserSiteId";
                ALTER TABLE orders DROP COLUMN IF EXISTS "UserSiteId";
                ALTER TABLE securitylogs DROP COLUMN IF EXISTS "UserSiteId";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "UserID",
                table: "UserSite",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.Sql("ALTER TABLE \"UserSite\" DROP CONSTRAINT IF EXISTS \"AK_UserSite_UserID\";");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_UserSite_UserID",
                table: "UserSite",
                column: "UserID");

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

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_securitylogs_userid" ON securitylogs (userid);
                CREATE INDEX IF NOT EXISTS "IX_orders_userid" ON orders (userid);
                """);

            migrationBuilder.CreateIndex(
                name: "ux_carts_userid",
                table: "carts",
                column: "userid",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_user_cart",
                table: "carts",
                column: "userid",
                principalTable: "UserSite",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_order",
                table: "orders",
                column: "userid",
                principalTable: "UserSite",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_logs",
                table: "securitylogs",
                column: "userid",
                principalTable: "UserSite",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_cart",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "fk_user_order",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_user_logs",
                table: "securitylogs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_UserSite_UserID",
                table: "UserSite");

            migrationBuilder.DropIndex(
                name: "ux_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ux_users_phone",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_securitylogs_userid",
                table: "securitylogs");

            migrationBuilder.DropIndex(
                name: "IX_orders_userid",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ux_carts_userid",
                table: "carts");

            migrationBuilder.AlterColumn<int>(
                name: "UserID",
                table: "UserSite",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "UserSiteId",
                table: "securitylogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserSiteId",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserSiteId",
                table: "carts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_securitylogs_UserSiteId",
                table: "securitylogs",
                column: "UserSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserSiteId",
                table: "orders",
                column: "UserSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_carts_UserSiteId",
                table: "carts",
                column: "UserSiteId");

            migrationBuilder.AddForeignKey(
                name: "FK_carts_UserSite_UserSiteId",
                table: "carts",
                column: "UserSiteId",
                principalTable: "UserSite",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_UserSite_UserSiteId",
                table: "orders",
                column: "UserSiteId",
                principalTable: "UserSite",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_securitylogs_UserSite_UserSiteId",
                table: "securitylogs",
                column: "UserSiteId",
                principalTable: "UserSite",
                principalColumn: "id");
        }
    }
}
