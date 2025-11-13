using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_QLTiemThuoc.Model.Thuoc
{
    [Table("GIATHUOC")]
    public class GiaThuoc
    {
        [Key]
        public string MaGiaThuoc { get; set; }

        public string MaThuoc { get; set; }

        public string MaLoaiDonVi { get; set; }

        public int SoLuong { get; set; }

        [Column(TypeName = "money")]
        public decimal DonGia { get; set; }

        public bool TrangThai { get; set; }
    }
}
