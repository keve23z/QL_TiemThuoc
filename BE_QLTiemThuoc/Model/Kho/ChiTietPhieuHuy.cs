using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_QLTiemThuoc.Model.Kho
{
    [Table("ChiTietPhieuHuy")]
    public class ChiTietPhieuHuy
    {
        [Key]
        [StringLength(20)]
        public string MaCTPH { get; set; } = null!;
        
        [Required]
        public bool LoaiHuy { get; set; }

        [Required]
        [StringLength(20)]
        public string MaPH { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string MaLo { get; set; } = null!;

        [Required]
        public int SoLuongHuy { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DonGia { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ThanhTien { get; set; }

        [StringLength(1000)]
        public string? GhiChu { get; set; }

        // Navigation to TonKho (the lot being referenced by MaLo)
        [ForeignKey("MaLo")]
        public virtual TonKho? TonKho { get; set; }
    }
}