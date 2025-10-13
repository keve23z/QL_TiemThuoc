using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace BE_QLTiemThuoc.Model
{
    public class Thuoc
    {
        [Key]
        public string MaThuoc { get; set; }

        public string MaLoaiThuoc { get; set; }

        // Remove or comment out this property if it does not exist in your database
        // public string LoaiThuoc { get; set; }

        [Required]
        public string TenThuoc { get; set; }

        public string ThanhPhan { get; set; }
        public string MoTa { get; set; }
        public string MaLoaiDonVi { get; set; }
        public int SoLuong { get; set; }
        public string CongDung { get; set; }
        public string CachDung { get; set; }
        public string LuuY { get; set; }
        public string UrlAnh { get; set; }
        public string MaNCC { get; set; }
        public decimal DonGiaSi { get; set; }
        public decimal DonGiaLe { get; set; }
    }
}