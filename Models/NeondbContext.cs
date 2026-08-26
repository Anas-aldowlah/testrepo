using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace YAGOT_2._0.Models;

public partial class NeondbContext : DbContext, IDataProtectionKeyContext
{
    public NeondbContext()
    {
    }

    public NeondbContext(DbContextOptions<NeondbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<Cartitem> Cartitems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Deliveryorder> Deliveryorders { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Orderdetail> Orderdetails { get; set; }

    public virtual DbSet<Orderitem> Orderitems { get; set; }

    public virtual DbSet<Paymentmethod> Paymentmethods { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductRetailPrice> ProductRetailPrices { get; set; }

    public virtual DbSet<Securitylog> Securitylogs { get; set; }

    public virtual DbSet<Storesetting> Storesettings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserSite> UserSites { get; set; }

    public virtual DbSet<Visit> Visits { get; set; }

    public virtual DbSet<SalesDay> SalesDays { get; set; }

    public virtual DbSet<Sale> Sales { get; set; }

    public virtual DbSet<SaleItem> SaleItems { get; set; }

    public virtual DbSet<SalePayment> SalePayments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(
                "Host=ep-floral-forest-al2seqtv.c-3.eu-central-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_KPysbZlLh54g;SSL Mode=Require;Trust Server Certificate=true;KeepAlive=30;TcpKeepAlive=true;Timeout=30;CommandTimeout=60",
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("carts_pkey");

            entity.ToTable("carts");

            entity.HasIndex(e => e.Userid, "ux_carts_userid").IsUnique();

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne<UserSite>().WithMany(e => e.Carts)
                .HasForeignKey(e => e.Userid)
                .HasPrincipalKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_cart");
        });

        modelBuilder.Entity<Cartitem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cartitems_pkey");

            entity.ToTable("cartitems", t =>
                t.HasCheckConstraint("ck_cartitems_quantity_nonnegative", "quantity >= 0"));

            entity.HasIndex(e => new { e.Cartid, e.Productid, e.RetailPriceId }, "ix_cartitems_cart_product_retail");

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.Cartid).HasColumnName("cartid");
            entity.Property(e => e.Productid).HasColumnName("productid");
            entity.Property(e => e.RetailPriceId).HasColumnName("retail_price_id");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");

            entity.HasOne(d => d.Cart).WithMany(p => p.Cartitems)
                .HasForeignKey(d => d.Cartid)
                .HasConstraintName("fk_cart");

            entity.HasOne(d => d.Product).WithMany(p => p.Cartitems)
                .HasForeignKey(d => d.Productid)
                .HasConstraintName("fk_product_cart");

            entity.HasOne(d => d.RetailPrice).WithMany(p => p.Cartitems)
                .HasForeignKey(d => d.RetailPriceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_cartitems_retail_price");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.ToTable("categories");

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Imageurl).HasColumnName("imageurl");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Deliveryorder>(entity =>
        {
            entity.HasKey(e => e.Orderid).HasName("deliveryorders_pkey");

            entity.ToTable("deliveryorders");

            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.District)
                .HasMaxLength(100)
                .HasColumnName("district");
            entity.Property(e => e.Fullname)
                .HasMaxLength(150)
                .HasColumnName("fullname");
            entity.Property(e => e.Governorate)
                .HasMaxLength(100)
                .HasColumnName("governorate");
            entity.Property(e => e.Phonenumber)
                .HasMaxLength(20)
                .HasColumnName("phonenumber");
            entity.Property(e => e.Secondphonenumber)
                .HasMaxLength(20)
                .HasColumnName("secondphonenumber");

            entity.HasOne<Order>().WithOne(p => p.Deliveryorder)
                .HasForeignKey<Deliveryorder>(d => d.Orderid);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable("orders", t =>
            {
                t.HasCheckConstraint("ck_orders_totalamount_nonnegative", "totalamount >= 0");
                t.HasCheckConstraint("ck_orders_refundrequiredamount_range", "refundrequiredamount IS NULL OR (refundrequiredamount >= 0 AND refundrequiredamount <= totalamount)");
                t.HasCheckConstraint("ck_orders_finalfulfilledamount_range", "finalfulfilledamount IS NULL OR (finalfulfilledamount >= 0 AND finalfulfilledamount <= totalamount)");
                t.HasCheckConstraint("ck_orders_financial_resolution_balance", "finalfulfilledamount IS NULL OR refundrequiredamount IS NULL OR finalfulfilledamount + refundrequiredamount = totalamount");
                t.HasCheckConstraint("ck_orders_payment_verification_pair", "(paymentverifiedat IS NULL) = (paymentverifiedbyuserid IS NULL)");
                t.HasCheckConstraint("ck_orders_workflowstate", "workflowstate IS NULL OR workflowstate IN ('ConflictAwaitingDecision', 'ConflictResolvedContinue', 'ConflictResolvedCancel')");
            });

            entity.HasIndex(e => new { e.Status, e.Orderdate }, "ix_orders_status_orderdate")
                .IsDescending(false, true);

            entity.HasIndex(e => new { e.Userid, e.Orderdate }, "ix_orders_userid_orderdate")
                .IsDescending(false, true);

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .HasColumnName("notes");
            entity.Property(e => e.Orderdate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("orderdate");
            entity.Property(e => e.Paymentmethod)
                .HasMaxLength(50)
                .HasColumnName("paymentmethod");
            entity.Property(e => e.Paymentstatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Unpaid'::character varying")
                .HasColumnName("paymentstatus");
            entity.Property(e => e.Paymentverifiedat)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("paymentverifiedat");
            entity.Property(e => e.Paymentverifiedbyuserid).HasColumnName("paymentverifiedbyuserid");
            entity.Property(e => e.Receipturl).HasColumnName("receipturl");
            entity.Property(e => e.Workflowstate)
                .HasMaxLength(50)
                .HasColumnName("workflowstate");
            entity.Property(e => e.Refundrequiredamount)
                .HasPrecision(10, 2)
                .HasColumnName("refundrequiredamount");
            entity.Property(e => e.Refundreason)
                .HasMaxLength(100)
                .HasColumnName("refundreason");
            entity.Property(e => e.Finalfulfilledamount)
                .HasPrecision(10, 2)
                .HasColumnName("finalfulfilledamount");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Stockdeducted)
                .HasDefaultValue(false)
                .HasColumnName("stockdeducted");
            entity.Property(e => e.TimeState)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Totalamount)
                .HasPrecision(10, 2)
                .HasColumnName("totalamount");
            entity.Property(e => e.Trackingnumber)
                .HasMaxLength(100)
                .HasColumnName("trackingnumber");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne<UserSite>().WithMany(e => e.Orders)
                .HasForeignKey(e => e.Userid)
                .HasPrincipalKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_order");
        });

        modelBuilder.Entity<Orderdetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orderdetails_pkey");

            entity.ToTable("orderdetails");

            entity.HasIndex(e => e.Orderid, "IX_orderdetails_orderid").IsUnique();

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.Accountname)
                .HasMaxLength(150)
                .HasColumnName("accountname");
            entity.Property(e => e.Accountnumber)
                .HasMaxLength(100)
                .HasColumnName("accountnumber");
            entity.Property(e => e.Createdat)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.District)
                .HasMaxLength(100)
                .HasColumnName("district");
            entity.Property(e => e.Fulladdress).HasColumnName("fulladdress");
            entity.Property(e => e.Governorate)
                .HasMaxLength(100)
                .HasColumnName("governorate");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Paymentimagepath).HasColumnName("paymentimagepath");
            entity.Property(e => e.Paymentmethod)
                .HasMaxLength(100)
                .HasColumnName("paymentmethod");
            entity.Property(e => e.Paymentnotes).HasColumnName("paymentnotes");
            entity.Property(e => e.Recipientname)
                .HasMaxLength(150)
                .HasColumnName("recipientname");
            entity.Property(e => e.Recipientphone)
                .HasMaxLength(50)
                .HasColumnName("recipientphone");
            entity.Property(e => e.Region)
                .HasMaxLength(100)
                .HasColumnName("region");
            entity.Property(e => e.Transferreferencenumber)
                .HasMaxLength(100)
                .HasColumnName("transferreferencenumber");
            entity.Property(e => e.Updatedat)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updatedat");

            entity.HasOne(d => d.Order).WithOne(p => p.Orderdetail)
                .HasForeignKey<Orderdetail>(d => d.Orderid)
                .HasConstraintName("fk_orderdetails_order");
        });

        modelBuilder.Entity<Orderitem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orderitems_pkey");

            entity.ToTable("orderitems", t =>
            {
                t.HasCheckConstraint("ck_orderitems_quantity_positive", "quantity > 0");
                t.HasCheckConstraint("ck_orderitems_unitprice_nonnegative", "unitprice >= 0");
                t.HasCheckConstraint("ck_orderitems_fulfilled_quantity_range", "fulfilled_quantity IS NULL OR (fulfilled_quantity >= 0 AND fulfilled_quantity <= quantity)");
                t.HasCheckConstraint("ck_orderitems_unavailable_quantity_range", "unavailable_quantity IS NULL OR (unavailable_quantity >= 0 AND unavailable_quantity <= quantity)");
            });

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Productid).HasColumnName("productid");
            entity.Property(e => e.RetailPriceId).HasColumnName("retail_price_id");
            entity.Property(e => e.RetailSizeMl).HasColumnName("retail_size_ml");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.FulfilledQuantity).HasColumnName("fulfilled_quantity");
            entity.Property(e => e.UnavailableQuantity).HasColumnName("unavailable_quantity");
            entity.Property(e => e.Unitprice)
                .HasPrecision(10, 2)
                .HasColumnName("unitprice");

            entity.HasOne(d => d.Order).WithMany(p => p.Orderitems)
                .HasForeignKey(d => d.Orderid)
                .HasConstraintName("fk_order");

            entity.HasOne(d => d.Product).WithMany(p => p.Orderitems)
                .HasForeignKey(d => d.Productid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_product_order");

            entity.HasOne(d => d.RetailPrice).WithMany(p => p.Orderitems)
                .HasForeignKey(d => d.RetailPriceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_orderitems_retail_price");
        });

        modelBuilder.Entity<Paymentmethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("paymentmethods_pkey");

            entity.ToTable("paymentmethods");

            entity.HasIndex(e => e.Storesettingsid, "ix_paymentmethods_storesettingsid");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Accountholdername)
                .HasMaxLength(200)
                .HasColumnName("accountholdername");
            entity.Property(e => e.Accountnumber)
                .HasMaxLength(200)
                .HasColumnName("accountnumber");
            entity.Property(e => e.Cardcolor)
                .HasMaxLength(20)
                .HasColumnName("cardcolor");
            entity.Property(e => e.Instructions).HasColumnName("instructions");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Storesettingsid)
                .HasDefaultValue(1)
                .HasColumnName("storesettingsid");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");

            entity.HasOne(d => d.Storesettings).WithMany(p => p.Paymentmethods)
                .HasForeignKey(d => d.Storesettingsid)
                .HasConstraintName("fk_paymentmethods_storesettings");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");

            entity.ToTable("products", t =>
            {
                t.HasCheckConstraint("ck_products_stock_unit", "stock_unit IN ('Piece', 'Ml')");
                t.HasCheckConstraint("ck_products_volume_for_ml", "(stock_unit = 'Piece' AND volume_ml IS NULL) OR (stock_unit = 'Ml' AND volume_ml IS NOT NULL AND volume_ml > 0)");
                t.HasCheckConstraint("ck_products_retail_requires_ml", "(is_retail_enabled = false) OR (stock_unit = 'Ml' AND volume_ml IS NOT NULL AND volume_ml > 0)");
                t.HasCheckConstraint("ck_products_price_nonnegative", "price >= 0");
                t.HasCheckConstraint("ck_products_stockquantity_nonnegative", "stockquantity >= 0");
            });

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.Brand)
                .HasMaxLength(150)
                .HasColumnName("brand");
            entity.Property(e => e.Categoryid).HasColumnName("categoryid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Imageurl).HasColumnName("imageurl");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(10, 2)
                .HasColumnName("price");
            entity.Property(e => e.StockUnit)
                .HasMaxLength(10)
                .HasDefaultValue("Piece")
                .HasColumnName("stock_unit");
            entity.Property(e => e.VolumeMl).HasColumnName("volume_ml");
            entity.Property(e => e.IsRetailEnabled)
                .HasDefaultValue(false)
                .HasColumnName("is_retail_enabled");
            entity.Property(e => e.Stockquantity).HasColumnName("stockquantity");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.Categoryid)
                .HasConstraintName("fk_category");
        });

        modelBuilder.Entity<ProductRetailPrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_retail_prices_pkey");

            entity.ToTable("product_retail_prices", t =>
            {
                t.HasCheckConstraint("ck_product_retail_prices_size_positive", "size_ml > 0");
                t.HasCheckConstraint("ck_product_retail_prices_price_positive", "price > 0");
            });

            entity.HasIndex(e => new { e.ProductId, e.SizeMl }, "ux_product_retail_prices_product_size").IsUnique();

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.SizeMl).HasColumnName("size_ml");
            entity.Property(e => e.Price)
                .HasPrecision(10, 2)
                .HasColumnName("price");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.HasOne(d => d.Product).WithMany(p => p.RetailPrices)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_product_retail_prices_product");
        });

        modelBuilder.Entity<Securitylog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("securitylogs_pkey");

            entity.ToTable("securitylogs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(255)
                .HasColumnName("action");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Deviceinfo).HasColumnName("deviceinfo");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(45)
                .HasColumnName("ipaddress");
            entity.Property(e => e.Riskscore)
                .HasDefaultValue(0)
                .HasColumnName("riskscore");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne<UserSite>().WithMany(e => e.Securitylogs)
                .HasForeignKey(e => e.Userid)
                .HasPrincipalKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_logs");
        });

        modelBuilder.Entity<Storesetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storesettings_pkey");

            entity.ToTable("storesettings");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Contactemail)
                .HasDefaultValueSql("''::text")
                .HasColumnName("contactemail");
            entity.Property(e => e.Featuredcategoryid).HasColumnName("featuredcategoryid");
            entity.Property(e => e.Heromarketingdesc)
                .HasDefaultValueSql("''::text")
                .HasColumnName("heromarketingdesc");
            entity.Property(e => e.Heromarketingtext)
                .HasDefaultValueSql("''::text")
                .HasColumnName("heromarketingtext");
            entity.Property(e => e.Instagramlink)
                .HasDefaultValueSql("''::text")
                .HasColumnName("instagramlink");
            entity.Property(e => e.Tiktoklink)
                .HasDefaultValueSql("''::text")
                .HasColumnName("tiktoklink");
            entity.Property(e => e.Twitterlink)
                .HasDefaultValueSql("''::text")
                .HasColumnName("twitterlink");
            entity.Property(e => e.Whatsappnumber)
                .HasDefaultValueSql("''::text")
                .HasColumnName("whatsappnumber");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "ux_users_email").IsUnique();
            entity.HasIndex(e => e.Phone, "ux_users_phone").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Email)
                .HasMaxLength(260)
                .HasColumnName("email");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Passwordhash).HasColumnName("passwordhash");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Customer'::character varying")
                .HasColumnName("role");
        });

        modelBuilder.Entity<UserSite>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("UserSite_pkey");

            entity.ToTable("UserSite");

            entity.HasAlternateKey(e => e.UserId).HasName("AK_UserSite_UserID");

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Role)
                .HasDefaultValueSql("'Customer'::character varying")
                .HasColumnType("character varying");
            entity.Property(e => e.UserId).HasColumnName("UserID");
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("visits_pkey");

            entity.ToTable("visits");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Browser)
                .HasMaxLength(30)
                .HasColumnName("browser");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .HasColumnName("country");
            entity.Property(e => e.Device)
                .HasMaxLength(20)
                .HasColumnName("device");
            entity.Property(e => e.Governorate)
                .HasMaxLength(100)
                .HasColumnName("governorate");
            entity.Property(e => e.Visitdate)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("visitdate");
            entity.Property(e => e.Visitorname)
                .HasMaxLength(100)
                .HasColumnName("visitorname");
        });

        modelBuilder.Entity<SalesDay>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sales_days_pkey");

            entity.ToTable("sales_days", t =>
            {
                t.HasCheckConstraint("ck_sales_days_opening_balance_nonnegative", "opening_balance >= 0");
                t.HasCheckConstraint("ck_sales_days_totals_nonnegative", "total_sales >= 0 AND total_cash >= 0 AND total_transfer >= 0 AND total_wallet >= 0 AND total_returns >= 0 AND net_total >= 0");
            });

            entity.HasIndex(e => new { e.CreatedBy, e.Status }, "ux_sales_days_created_by_open")
                .IsUnique()
                .HasFilter("status = 'Open'");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Date)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Open'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.OpeningBalance)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("opening_balance");
            entity.Property(e => e.TotalSales)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_sales");
            entity.Property(e => e.TotalCash)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_cash");
            entity.Property(e => e.TotalTransfer)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_transfer");
            entity.Property(e => e.TotalWallet)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_wallet");
            entity.Property(e => e.TotalReturns)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_returns");
            entity.Property(e => e.NetTotal)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("net_total");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(150)
                .HasColumnName("created_by");
            entity.Property(e => e.ClosedBy)
                .HasMaxLength(150)
                .HasColumnName("closed_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ClosedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("closed_at");
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sales_pkey");

            entity.ToTable("sales", t =>
                t.HasCheckConstraint(
                    "ck_sales_amounts_nonnegative",
                    "total_amount >= 0 AND discount_total >= 0 AND final_amount >= 0"));

            entity.HasIndex(e => e.SalesDayId, "ix_sales_sales_day_id");
            entity.HasIndex(e => e.InvoiceNumber, "ix_sales_invoice_number").IsUnique();
            entity.HasIndex(e => new { e.SalesDayId, e.Status }, "ix_sales_sales_day_status");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SalesDayId).HasColumnName("sales_day_id");
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100)
                .HasColumnName("invoice_number");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(150)
                .HasColumnName("customer_name");
            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(50)
                .HasColumnName("customer_phone");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total_amount");
            entity.Property(e => e.DiscountTotal)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("discount_total");
            entity.Property(e => e.FinalAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("final_amount");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Draft'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(150)
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("completed_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.DraftRevision)
                .HasDefaultValue(0L)
                .HasColumnName("draft_revision");
            entity.Property(e => e.EditSessionId).HasColumnName("edit_session_id");
            entity.Property(e => e.EditLockedBy)
                .HasMaxLength(256)
                .HasColumnName("edit_locked_by");
            entity.Property(e => e.EditLockExpiresAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("edit_lock_expires_at");

            entity.HasOne(d => d.SalesDay).WithMany(p => p.Sales)
                .HasForeignKey(d => d.SalesDayId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sales_sales_day");
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sale_items_pkey");

            entity.ToTable("sale_items", t =>
            {
                t.HasCheckConstraint("CK_SaleItem_Quantity_Positive", "quantity > 0");
                t.HasCheckConstraint("ck_sale_items_amounts_nonnegative", "unit_price >= 0 AND discount >= 0 AND total >= 0");
            });

            entity.HasIndex(e => e.SaleId, "ix_sale_items_sale_id");
            entity.HasIndex(e => e.ProductId, "ix_sale_items_product_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SaleId).HasColumnName("sale_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.RetailPriceId).HasColumnName("retail_price_id");
            entity.Property(e => e.RetailSizeMl).HasColumnName("retail_size_ml");
            entity.Property(e => e.ProductName)
                .HasMaxLength(255)
                .HasColumnName("product_name");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("unit_price");
            entity.Property(e => e.Discount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("discount");
            entity.Property(e => e.Total)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("total");

            entity.HasOne(d => d.Sale).WithMany(p => p.SaleItems)
                .HasForeignKey(d => d.SaleId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_sale_items_sale");

            entity.HasOne(d => d.Product).WithMany(p => p.SaleItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sale_items_product");

            entity.HasOne(d => d.RetailPrice).WithMany(p => p.SaleItems)
                .HasForeignKey(d => d.RetailPriceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_sale_items_retail_price");
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sale_payments_pkey");

            entity.ToTable("sale_payments", t => t.HasCheckConstraint("CK_SalePayment_Amount_Positive", "amount > 0"));

            entity.HasIndex(e => e.SaleId, "ix_sale_payments_sale_id");
            entity.HasIndex(e => e.PaymentMethodId, "ix_sale_payments_payment_method_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SaleId).HasColumnName("sale_id");
            entity.Property(e => e.PaymentMethodId).HasColumnName("payment_method_id");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("amount");
            entity.Property(e => e.TransactionReference)
                .HasMaxLength(150)
                .HasColumnName("transaction_reference");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Sale).WithMany(p => p.SalePayments)
                .HasForeignKey(d => d.SaleId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_sale_payments_sale");

            entity.HasOne(d => d.PaymentMethod).WithMany(p => p.SalePayments)
                .HasForeignKey(d => d.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sale_payments_payment_method");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
