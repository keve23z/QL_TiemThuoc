namespace BE_QLTiemThuoc.Dto
{
    public class UpdateHoaDonStatusDto
    {
        public string? MaHD { get; set; }

        // New status value for TrangThaiGiaoHang (e.g. -1: canceled, 0: placed, 1: confirmed, 2: delivered, 3: received)
        public int TrangThaiGiaoHang { get; set; }
    }
}
