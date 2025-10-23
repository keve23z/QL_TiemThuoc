namespace BE_QLTiemThuoc.Model.Kho.Dto
{
    using System;
    using System.Collections.Generic;

    public class PhieuNhapDto
    {
        // Allow MaPN to be omitted by the caller so the server can generate it when null/empty
        public string? MaPN { get; set; }
        public DateTime NgayNhap { get; set; }
        public string? MaNCC { get; set; }
        public string? MaNV { get; set; }
        public decimal? TongTien { get; set; }
        public string? GhiChu { get; set; }

        public List<ChiTietPhieuNhapDto> ChiTietPhieuNhaps { get; set; } = new();
        // LoThuocHSDs is optional; if missing, server will generate from ChiTietPhieuNhaps
        public List<LoThuocHSD>? LoThuocHSDs { get; set; } = null;
    }
}
