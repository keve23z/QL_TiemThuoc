using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_QLTiemThuoc.Model.Kho
{
    [Table("PhieuHuy")]
    public class PhieuHuy
    {
        [Key]
        [StringLength(20)]
        public string MaPH { get; set; } = null!;

        [Required]
        public DateTime NgayHuy { get; set; } = DateTime.Now;

        [Required]
        [StringLength(10)]
        public string MaNV { get; set; } = null!;

        [Required]
        [StringLength(10)]
        public string LoaiHuy { get; set; } = null!; // "KHO" hoặc "HOADON"

        [StringLength(20)]
        public string? MaHD { get; set; } // Mã hóa đơn (nếu hủy từ hóa đơn)

        [Column(TypeName = "decimal(18,2)")]
        public decimal TongSoLuongHuy { get; set; } = 0;

        public string? GhiChu { get; set; }

        // Navigation properties
        public virtual ICollection<ChiTietPhieuHuy> ChiTietPhieuHuys { get; set; } = new List<ChiTietPhieuHuy>();
    }
}