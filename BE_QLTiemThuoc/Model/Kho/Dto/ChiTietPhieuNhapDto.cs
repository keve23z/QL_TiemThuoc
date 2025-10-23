using System;

namespace BE_QLTiemThuoc.Model.Kho.Dto
{
    public class ChiTietPhieuNhapDto
    {
        public string? MaCTPN { get; set; }
        public string? MaThuoc { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }

        // additional fields coming from FE
        public DateTime? HanSuDung { get; set; }
        public string? MaLoaiDonViNhap { get; set; }
    }
}
