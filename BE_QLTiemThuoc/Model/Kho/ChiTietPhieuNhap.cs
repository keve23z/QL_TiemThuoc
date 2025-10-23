using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_QLTiemThuoc.Model.Kho
{
    [Table("ChiTietPhieuNhap")]
    public class ChiTietPhieuNhap
    {
        [Key]
        [StringLength(20)]
        public string MaCTPN { get; set; } = null!;

        [StringLength(20)]
        public string MaPN { get; set; } = null!;

        [StringLength(10)]
        public string MaThuoc { get; set; } = null!;

        public int SoLuong { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DonGia { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ThanhTien { get; set; }
    }
}