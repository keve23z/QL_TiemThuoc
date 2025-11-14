using System.Collections.Generic;

namespace BE_QLTiemThuoc.Dto
{
    public class HoaDonCreateDto
    {
        public string? MaKH { get; set; }
        public string? MaNV { get; set; }
        public string? GhiChu { get; set; }
        public decimal? TongTien { get; set; }
        public List<ChiTietHoaDonCreateDto>? Items { get; set; }
    }
}
