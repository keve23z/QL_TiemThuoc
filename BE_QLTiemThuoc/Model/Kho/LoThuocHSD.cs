using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_QLTiemThuoc.Model.Kho
{
    [Table("LoThuocHSD")]
    public class LoThuocHSD
    {
        [Key]
        [StringLength(20)]
        public string MaLoThuocHSD { get; set; } = null!;

        [StringLength(10)]
        public string MaThuoc { get; set; } = null!;

        [StringLength(20)]
        public string MaCTPN { get; set; } = null!;

        [Required]
        public DateTime HanSuDung { get; set; }

        public int SoLuong { get; set; }

        [StringLength(10)]
        public string MaLoaiDonViNhap { get; set; } = null!;

    }
}