using System.Text.Json.Serialization;

namespace FE_QLTiemThuoc.Models
{
    public class ThuocViewModel
    {
        [JsonPropertyName("maThuoc")]
        public string MaThuoc { get; set; }

        [JsonPropertyName("maLoaiThuoc")]
        public string MaLoaiThuoc { get; set; }

        [JsonPropertyName("tenThuoc")]
        public string TenThuoc { get; set; }

        [JsonPropertyName("moTa")]
        public string MoTa { get; set; }

        [JsonPropertyName("urlAnh")]
        public string UrlAnh { get; set; }

        [JsonPropertyName("donGiaSi")]
        public decimal DonGiaSi { get; set; }
    }
}