using System.Collections.Generic;

namespace BE_QLTiemThuoc.Dto
{
    // DTO for creating online invoices (HDOL). Kept small to show client-visible fields.
    public class HoaDonCreateOLDto
    {
        public string? MaKH { get; set; }
        public string? GhiChu { get; set; }
        public decimal? TongTien { get; set; }
        public List<ChiTietHoaDonCreateOLDto>? Items { get; set; }
    }
}
