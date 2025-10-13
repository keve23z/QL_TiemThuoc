using System.ComponentModel.DataAnnotations;

namespace BE_QLTiemThuoc.Model
{
    public class NhanVien
    {
        [Key]
        public string MANV { get; set; }
        public string? HoTen { get; set; }
        public DateTime? NgaySinh { get; set; }  // <- Có thể null
        public string? GioiTinh { get; set; }
        public string? DiaChi { get; set; }
        public string? DienThoai { get; set; }

        public string? ChucVu { get; set; }
    }
}
