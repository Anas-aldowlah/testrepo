using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.artifacts.scaffold-test.Models;

namespace YAGOT_2._0.artifacts.scaffold-test.Data;

public partial class TempScaffoldContext : DbContext
{
    public TempScaffoldContext(DbContextOptions<TempScaffoldContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Category> Categories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
