using System;
using System.Collections.Generic;

namespace FE_QLTiemThuoc.Models
{
    public class PhieuNhapDto
    {
        public string? MaPN { get; set; }
        public DateTime NgayNhap { get; set; }
        public string? MaNCC { get; set; }
        public string? MaNV { get; set; }
        public decimal? TongTien { get; set; }
        public string? GhiChu { get; set; }

        public List<ChiTietPhieuNhapDto> ChiTietPhieuNhaps { get; set; } = new();
    }
}
