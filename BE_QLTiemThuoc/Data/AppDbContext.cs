using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;

using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Model.Kho;

namespace BE_QLTiemThuoc.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TaiKhoan> TaiKhoans { get; set; }
        public DbSet<KhachHang> KhachHangs { get; set; }
        public DbSet<Thuoc> Thuoc { get; set; } // Add this DbSet for Thuoc
        public DbSet<NhaCungCap> NhaCungCaps { get; set; }
        public DbSet<LoaiThuoc> LoaiThuoc { get; set; }
        public DbSet<LoaiDonVi> LoaiDonVi { get; set; }
        public DbSet<NhomLoai> NhomLoai { get; set; }
        public DbSet<PhieuNhap> PhieuNhaps { get; set; } // Add this property
        public DbSet<ChiTietPhieuNhap> ChiTietPhieuNhaps { get; set; }
        public DbSet<LoThuocHSD> LoThuocHSDs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaiKhoan>().ToTable("TaiKhoan");
            modelBuilder.Entity<KhachHang>().ToTable("KhachHang");   
            modelBuilder.Entity<Thuoc>().ToTable("Thuoc");
            modelBuilder.Entity<NhaCungCap>().ToTable("NhaCungCap");
            modelBuilder.Entity<LoaiThuoc>().ToTable("LoaiThuoc");
            modelBuilder.Entity<LoaiDonVi>().ToTable("LoaiDonVi");
            modelBuilder.Entity<NhomLoai>().ToTable("NhomLoai");
            modelBuilder.Entity<PhieuNhap>().ToTable("PhieuNhap");
            modelBuilder.Entity<ChiTietPhieuNhap>().ToTable("ChiTietPhieuNhap");
            modelBuilder.Entity<LoThuocHSD>().ToTable("LoThuocHSD");

            //modelBuilder.Entity<DanhMucNguoiDung>()
            //    .HasOne(dm => dm.KhachHang)
            //    .WithMany()
            //    .HasForeignKey(dm => dm.MaNguoiDung)
            //    .OnDelete(DeleteBehavior.Restrict);

            //modelBuilder.Entity<DanhMuc>()
            //    .HasOne(dm => dm.Loai)
            //    .WithMany()
            //    .HasForeignKey(dm => dm.MaLoaiDanhMuc)
            //    .OnDelete(DeleteBehavior.Restrict);

            // Additional model configurations can go here
        }
    }

}
