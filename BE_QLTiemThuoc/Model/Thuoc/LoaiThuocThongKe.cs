namespace BE_QLTiemThuoc.Model.Thuoc
{
    public class LoaiThuocThongKe
    {
        public string MaLoaiThuoc { get; set; } = null!;
        public string TenLoaiThuoc { get; set; } = null!;
        public string Icon { get; set; } = null!;
        public int SoLuongThuoc { get; set; }
    }
}
