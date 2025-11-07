namespace BE_QLTiemThuoc.Dto
{
    public class ChiTietHoaDonCreateDto
    {
        // MaThuoc or TenThuoc should be provided (prefer MaThuoc if present)
        public string? MaThuoc { get; set; }
        public string? TenThuoc { get; set; }
        // Sale type: 0 = Sỉ (sealed packages), 1 = Lẻ (loose units)
        public int? TrangThaiSeal { get; set; }
        // The unit selected by user (e.g., "Hộp", "Vỉ", "Viên")
        public string? DonVi { get; set; }
        // Quantity to sell (packages when TrangThaiSeal = 0, units when = 1)
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public string? MaLD { get; set; }
    }
}
