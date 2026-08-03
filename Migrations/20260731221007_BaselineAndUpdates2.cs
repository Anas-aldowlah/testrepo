using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class BaselineAndUpdates2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    imageurl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("categories_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deliveryorders",
                columns: table => new
                {
                    orderid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fullname = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phonenumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    secondphonenumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("deliveryorders_pkey", x => x.orderid);
                });

            migrationBuilder.CreateTable(
                name: "storesettings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    whatsappnumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    contactemail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instagramlink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    twitterlink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tiktoklink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    brandsmarquee = table.Column<string>(type: "text", nullable: false),
                    featuredcategoryid = table.Column<int>(type: "integer", nullable: true),
                    heromarketingtext = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    heromarketingdesc = table.Column<string>(type: "text", nullable: false),
                    omqiaccountname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    omqiaccountnumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    busairiaccountname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    busairiaccountnumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bindowalaccountname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    bindowalaccountnumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    otherpaymentinstructions = table.Column<string>(type: "text", nullable: false),
                    shippingfee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    freeshippingthreshold = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("storesettings_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "UserSite",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying", nullable: true, defaultValueSql: "'Customer'::character varying")
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserSite_pkey", x => x.id);
                    table.UniqueConstraint("AK_UserSite_UserID", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "visits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    visitdate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)"),
                    visitorname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    device = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    browser = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("visits_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    categoryid = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    stockquantity = table.Column<int>(type: "integer", nullable: false),
                    imageurl = table.Column<string>(type: "text", nullable: true),
                    brand = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("products_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_category",
                        column: x => x.categoryid,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("carts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_cart",
                        column: x => x.userid,
                        principalTable: "UserSite",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    orderdate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    totalamount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    trackingnumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TimeState = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "(CURRENT_TIMESTAMP + '03:00:00'::interval)"),
                    paymentmethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    paymentstatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Unpaid"),
                    receipturl = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("orders_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_order",
                        column: x => x.userid,
                        principalTable: "UserSite",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "securitylogs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ipaddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    deviceinfo = table.Column<string>(type: "text", nullable: true),
                    riskscore = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    createdat = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("securitylogs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_logs",
                        column: x => x.userid,
                        principalTable: "UserSite",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cartitems",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cartid = table.Column<int>(type: "integer", nullable: false),
                    productid = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cartitems_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_cart",
                        column: x => x.cartid,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_cart",
                        column: x => x.productid,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orderitems",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orderid = table.Column<int>(type: "integer", nullable: false),
                    productid = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unitprice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("orderitems_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_order",
                        column: x => x.orderid,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_order",
                        column: x => x.productid,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cartitems_cartid",
                table: "cartitems",
                column: "cartid");

            migrationBuilder.CreateIndex(
                name: "IX_cartitems_productid",
                table: "cartitems",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_carts_userid",
                table: "carts",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_orderitems_orderid",
                table: "orderitems",
                column: "orderid");

            migrationBuilder.CreateIndex(
                name: "IX_orderitems_productid",
                table: "orderitems",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_orders_userid",
                table: "orders",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_products_categoryid",
                table: "products",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_securitylogs_userid",
                table: "securitylogs",
                column: "userid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cartitems");

            migrationBuilder.DropTable(
                name: "deliveryorders");

            migrationBuilder.DropTable(
                name: "orderitems");

            migrationBuilder.DropTable(
                name: "securitylogs");

            migrationBuilder.DropTable(
                name: "storesettings");

            migrationBuilder.DropTable(
                name: "visits");

            migrationBuilder.DropTable(
                name: "carts");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "UserSite");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
