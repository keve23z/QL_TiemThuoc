using System.Text.Json.Serialization;

namespace FE_QLTiemThuoc.Models
{
    public class LoaiThuoc
    {
        [JsonPropertyName("maLoaiThuoc")]
        public string MaLoaiThuoc { get; set; }

        [JsonPropertyName("tenLoaiThuoc")]
        public string TenLoaiThuoc { get; set; }

        [JsonPropertyName("maNhomLoai")]
        public string MaNhomLoai { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }
    }
}