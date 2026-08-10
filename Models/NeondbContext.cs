using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace YAGOT_2._0.Models;

public partial class NeondbContext : DbContext
{
    public NeondbContext()
    {
    }

    public NeondbContext(DbContextOptions<NeondbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<Cartitem> Cartitems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Deliveryorder> Deliveryorders { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Orderdetail> Orderdetails { get; set; }

    public virtual DbSet<Orderitem> Orderitems { get; set; }

    public virtual DbSet<Paymentmethod> Paymentmethods { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Securitylog> Securitylogs { get; set; }

    public virtual DbSet<Storesetting> Storesettings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserSite> UserSites { get; set; }

    public virtual DbSet<Visit> Visits { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=ep-floral-forest-al2seqtv.c-3.eu-central-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_KPysbZlLh54g;SSL Mode=Require;Trust Server Certificate=true");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("carts_pkey");

            entity.ToTable("carts");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(CURRENT_TIMESTAMP + '03:00:00'::interval)")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Userid).HasColumnName("userid");
        });

        modelBuilder.Entity<Cartitem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cartitems_pkey");

            entity.ToTable("cartitems");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Cartid).HasColumnName("cartid");
            entity.Property(e => e.Productid).HasColumnName("productid");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");

            entity.HasOne(d => d.Cart).WithMany(p => p.Cartitems)
                .HasForeignKey(d => d.Cartid)
                .HasConstraintName("fk_cart");

            entity.HasOne(d => d.Product).WithMany(p => p.Cartitems)
                .HasForeignKey(d => d.Productid)
                .HasConstraintName("fk_product_cart");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.ToTable("categories");

            entity.Property(e => e.Id).HasColumnName("id");
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
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.Property(e => e.Id).HasColumnName("id");
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
            entity.Property(e => e.Receipturl).HasColumnName("receipturl");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
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
        });

        modelBuilder.Entity<Orderdetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orderdetails_pkey");

            entity.ToTable("orderdetails");

            entity.HasIndex(e => e.Orderid, "IX_orderdetails_orderid").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
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

            entity.ToTable("orderitems");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Orderid).HasColumnName("orderid");
            entity.Property(e => e.Productid).HasColumnName("productid");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
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

            entity.ToTable("products");

            entity.Property(e => e.Id).HasColumnName("id");
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
            entity.Property(e => e.Stockquantity).HasColumnName("stockquantity");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.Categoryid)
                .HasConstraintName("fk_category");
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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
