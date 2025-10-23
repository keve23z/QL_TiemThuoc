using System;

namespace FE_QLTiemThuoc.Models
{
    public class LoThuocHSDDto
    {
        public string? MaLoThuocHSD { get; set; }
        public string? MaThuoc { get; set; }
        public string? MaCTPN { get; set; }
        public DateTime HanSuDung { get; set; }
        public int SoLuong { get; set; }
        public string? MaLoaiDonViNhap { get; set; }
    }
}
