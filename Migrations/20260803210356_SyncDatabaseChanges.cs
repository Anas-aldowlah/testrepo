using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class SyncDatabaseChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "IX_securitylogs_userid",
                table: "securitylogs");

            migrationBuilder.DropIndex(
                name: "IX_orders_userid",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_carts_userid",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "bindowalaccountname",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "bindowalaccountnumber",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "brandsmarquee",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "busairiaccountname",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "busairiaccountnumber",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "featuredcategoryid",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "freeshippingthreshold",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "heromarketingdesc",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "heromarketingtext",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "omqiaccountname",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "omqiaccountnumber",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "otherpaymentinstructions",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "shippingfee",
                table: "storesettings");

            migrationBuilder.AlterColumn<int>(
                name: "UserID",
                table: "UserSite",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "whatsappnumber",
                table: "storesettings",
                type: "text",
                nullable: true,
                defaultValueSql: "''::text",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "twitterlink",
                table: "storesettings",
                type: "text",
                nullable: true,
                defaultValueSql: "''::text",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "tiktoklink",
                table: "storesettings",
                type: "text",
                nullable: true,
                defaultValueSql: "''::text",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "instagramlink",
                table: "storesettings",
                type: "text",
                nullable: true,
                defaultValueSql: "''::text",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "contactemail",
                table: "storesettings",
                type: "text",
                nullable: true,
                defaultValueSql: "''::text",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<int>(
                name: "UserSiteId",
                table: "securitylogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "paymentstatus",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValueSql: "'Unpaid'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Unpaid");

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

            migrationBuilder.CreateTable(
                name: "orderdetails",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orderid = table.Column<int>(type: "integer", nullable: false),
                    recipientname = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    recipientphone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fulladdress = table.Column<string>(type: "text", nullable: true),
                    paymentmethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    accountname = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    accountnumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transferreferencenumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paymentimagepath = table.Column<string>(type: "text", nullable: true),
                    paymentnotes = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updatedat = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("orderdetails_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_orderdetails_order",
                        column: x => x.orderid,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paymentmethods",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accountholdername = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accountnumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: true),
                    cardcolor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    isactive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    storesettingsid = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("paymentmethods_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_paymentmethods_storesettings",
                        column: x => x.storesettingsid,
                        principalTable: "storesettings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    passwordhash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Customer'::character varying"),
                    createdat = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    email = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_orderdetails_orderid",
                table: "orderdetails",
                column: "orderid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_paymentmethods_storesettingsid",
                table: "paymentmethods",
                column: "storesettingsid");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_carts_UserSite_UserSiteId",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_UserSite_UserSiteId",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_securitylogs_UserSite_UserSiteId",
                table: "securitylogs");

            migrationBuilder.DropTable(
                name: "orderdetails");

            migrationBuilder.DropTable(
                name: "paymentmethods");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_securitylogs_UserSiteId",
                table: "securitylogs");

            migrationBuilder.DropIndex(
                name: "IX_orders_UserSiteId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_carts_UserSiteId",
                table: "carts");

            migrationBuilder.DropColumn(
                name: "UserSiteId",
                table: "securitylogs");

            migrationBuilder.DropColumn(
                name: "UserSiteId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "UserSiteId",
                table: "carts");

            migrationBuilder.AlterColumn<int>(
                name: "UserID",
                table: "UserSite",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "whatsappnumber",
                table: "storesettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValueSql: "''::text");

            migrationBuilder.AlterColumn<string>(
                name: "twitterlink",
                table: "storesettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValueSql: "''::text");

            migrationBuilder.AlterColumn<string>(
                name: "tiktoklink",
                table: "storesettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValueSql: "''::text");

            migrationBuilder.AlterColumn<string>(
                name: "instagramlink",
                table: "storesettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValueSql: "''::text");

            migrationBuilder.AlterColumn<string>(
                name: "contactemail",
                table: "storesettings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValueSql: "''::text");

            migrationBuilder.AddColumn<string>(
                name: "bindowalaccountname",
                table: "storesettings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "bindowalaccountnumber",
                table: "storesettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "brandsmarquee",
                table: "storesettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "busairiaccountname",
                table: "storesettings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "busairiaccountnumber",
                table: "storesettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "featuredcategoryid",
                table: "storesettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "freeshippingthreshold",
                table: "storesettings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "heromarketingdesc",
                table: "storesettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "heromarketingtext",
                table: "storesettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "omqiaccountname",
                table: "storesettings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "omqiaccountnumber",
                table: "storesettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "otherpaymentinstructions",
                table: "storesettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "shippingfee",
                table: "storesettings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "paymentstatus",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unpaid",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValueSql: "'Unpaid'::character varying");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_UserSite_UserID",
                table: "UserSite",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_securitylogs_userid",
                table: "securitylogs",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_orders_userid",
                table: "orders",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_carts_userid",
                table: "carts",
                column: "userid");

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
    }
}
