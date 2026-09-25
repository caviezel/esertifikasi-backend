using Esertifikasi.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api;

internal static class AdministrativeRegionModelConfiguration {
  public static void ConfigureAdministrativeRegionModel(this ModelBuilder modelBuilder) {
    modelBuilder.Entity<Province>(entity => {
      entity.ToTable("Province");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Code).HasMaxLength(2).IsRequired();
      entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
      entity.HasIndex(x => x.Code).IsUnique();
      entity.HasIndex(x => x.Name);
    });
    modelBuilder.Entity<Regency>(entity => {
      entity.ToTable("Regency");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Code).HasMaxLength(5).IsRequired();
      entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
      entity.HasIndex(x => x.Code).IsUnique();
      entity.HasIndex(x => new { x.ProvinceId, x.Name });
      entity.HasOne(x => x.Province).WithMany(x => x.Regencies).HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<District>(entity => {
      entity.ToTable("District");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Code).HasMaxLength(8).IsRequired();
      entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
      entity.HasIndex(x => x.Code).IsUnique();
      entity.HasIndex(x => new { x.RegencyId, x.Name });
      entity.HasOne(x => x.Regency).WithMany(x => x.Districts).HasForeignKey(x => x.RegencyId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<Village>(entity => {
      entity.ToTable("Village");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Code).HasMaxLength(13).IsRequired();
      entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
      entity.HasIndex(x => x.Code).IsUnique();
      entity.HasIndex(x => new { x.DistrictId, x.Name });
      entity.HasOne(x => x.District).WithMany(x => x.Villages).HasForeignKey(x => x.DistrictId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<RegionDatasetImport>(entity => {
      entity.ToTable("RegionDatasetImport");
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Version).HasMaxLength(100).IsRequired();
      entity.Property(x => x.Source).HasMaxLength(500).IsRequired();
      entity.Property(x => x.DatasetSha256).HasMaxLength(64).IsRequired();
      entity.HasIndex(x => x.Version).IsUnique();
      entity.HasIndex(x => x.DatasetSha256);
    });
  }
}
