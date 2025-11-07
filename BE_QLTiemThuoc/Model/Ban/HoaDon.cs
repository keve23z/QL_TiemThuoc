using System;

namespace BE_QLTiemThuoc.Model
{
    public class HoaDon
    {
        public string MaHD { get; set; } = string.Empty;
        public DateTime NgayLap { get; set; }
        public string? MaKH { get; set; }
        public string? MaNV { get; set; }
        public decimal TongTien { get; set; }
        public string? GhiChu { get; set; }
        public int? TrangThaiGiaoHang { get; set; }
    }
}
