using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models.UsersDatabase;

namespace YAGOT_2._0.Data;

public partial class UsersDbContext : DbContext
{
    public UsersDbContext()
    {
    }

    public UsersDbContext(DbContextOptions<UsersDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(
                "Host=ep-misty-frost-afopimuh-pooler.c-2.us-west-2.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_1GQVFO3lXnUS;SSL Mode=Require;Channel Binding=Require;Trust Server Certificate=true;KeepAlive=30;TcpKeepAlive=true;Timeout=30;CommandTimeout=60",
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "ux_users_email").IsUnique();
            entity.HasIndex(e => e.Phone, "ux_users_phone").IsUnique();

            entity.Property(e => e.Id).UseIdentityByDefaultColumn().HasColumnName("id");
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
        });
    }
}
