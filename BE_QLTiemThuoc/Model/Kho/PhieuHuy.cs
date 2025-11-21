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
        public bool? LoaiHuy { get; set; } // bit: 0 = KHO, 1 = HOADON

        [StringLength(20)]
        public string? MaHD { get; set; } // Mã hóa đơn (nếu hủy từ hóa đơn)

        public int? TongMatHangHuy { get; set; }
        public int? TongSoLuongHuy { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TongTienHuy { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TongTienKho { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TongTien { get; set; }

        public string? GhiChu { get; set; }

        // Navigation properties
        public virtual ICollection<ChiTietPhieuHuy> ChiTietPhieuHuys { get; set; } = new List<ChiTietPhieuHuy>();
    }
}