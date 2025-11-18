using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_QLTiemThuoc.Model.Kho
{
    [Table("ChiTietPhieuHuy")]
    public class ChiTietPhieuHuy
    {
        [Key, Column(Order = 0)]
        [StringLength(20)]
        public string MaPH { get; set; } = null!;

        [Key, Column(Order = 1)]
        [StringLength(20)]
        public string MaLo { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SoLuongHuy { get; set; }

        [Required]
        [StringLength(500)]
        public string LyDoHuy { get; set; } = null!;

        public string? GhiChu { get; set; }

        // Navigation properties
        public virtual PhieuHuy PhieuHuy { get; set; } = null!;
        public virtual TonKho TonKho { get; set; } = null!;
    }
}