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
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=ep-misty-frost-afopimuh-pooler.c-2.us-west-2.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_1GQVFO3lXnUS;SSL Mode=Require;Channel Binding=Require;Trust Server Certificate=true");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        });
    }
}