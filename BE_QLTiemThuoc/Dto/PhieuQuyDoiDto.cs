namespace BE_QLTiemThuoc.Dto
{
    public class PhieuQuyDoiItemDto
    {
        // Now we accept MaThuoc (medicine code) instead of a specific MaLoGoc.
        // The service will consume lots (MaLo) in HSD order to fulfill SoLuongGoc.
        public string? MaThuoc { get; set; }
        public int SoLuongGoc { get; set; }
        // optional override; if null service will use THUOC.SoLuong
        public int? UnitsPerPackage { get; set; }
        // optional DonViMoi per item
        public string? DonViMoi { get; set; }
        public string? GhiChu { get; set; }
    }

    // Batch create: one phieu with many items
    public class PhieuQuyDoiBatchCreateDto
    {
        public List<PhieuQuyDoiItemDto>? Items { get; set; }
        public string? MaNV { get; set; }
        public string? GhiChu { get; set; }
    }

    // Quick convert by MaThuoc (single package) - simplified DTO: only MaThuoc and optional DonViMoi
    public class PhieuQuyDoiQuickByMaDto
    {
        public string MaThuoc { get; set; } = string.Empty;
        // optional target unit (e.g. "Viên")
        public string? DonViMoi { get; set; }
    }

    

    
}
