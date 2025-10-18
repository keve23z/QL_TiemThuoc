using System.Text.Json.Serialization;

namespace FE_QLTiemThuoc.Models
{
    public class NhaCungCap
    {
        [JsonPropertyName("maNCC")]
        public string MaNCC { get; set; }

        [JsonPropertyName("tenNCC")]
        public string TenNCC { get; set; }

        [JsonPropertyName("diaChi")]
        public string DiaChi { get; set; }

        [JsonPropertyName("dienThoai")]
        public string DienThoai { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
    }
}
