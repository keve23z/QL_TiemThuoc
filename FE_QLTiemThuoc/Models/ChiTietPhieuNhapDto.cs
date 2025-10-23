using System;

namespace FE_QLTiemThuoc.Models
{
    public class ChiTietPhieuNhapDto
    {
        public string? MaCTPN { get; set; }
        public string? MaThuoc { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }

        // added: expiry and unit per chi tiet (moved from Lo)
        public DateTime HanSuDung { get; set; }
        public string? MaLoaiDonViNhap { get; set; }
    }
}
