using System.ComponentModel.DataAnnotations;

namespace BE_QLTiemThuoc.Dto
{
    public class PhieuHuyDto
    {
        [Required]
        public string MaNV { get; set; } = null!;

        [Required]
        public string LoaiHuy { get; set; } = null!; // "KHO" hoặc "HOADON"

        public string? MaHD { get; set; } // Mã hóa đơn (nếu hủy từ hóa đơn)

        public string? GhiChu { get; set; }

        [Required]
        public List<ChiTietPhieuHuyDto> ChiTietPhieuHuys { get; set; } = new List<ChiTietPhieuHuyDto>();
    }

    public class ChiTietPhieuHuyDto
    {
        [Required]
        public string MaLo { get; set; } = null!;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số lượng hủy phải lớn hơn 0")]
        public decimal SoLuongHuy { get; set; }

        [Required]
        [StringLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự")]
        public string LyDoHuy { get; set; } = null!;

        public string? GhiChu { get; set; }

        // Trường mới: Loại xử lý cho từng thuốc trong hóa đơn
        public string LoaiXuLy { get; set; } = "HUY"; // "HUY" hoặc "HOAN_LAI"
    }

    // DTO mới cho việc hủy hóa đơn với xử lý linh hoạt
    public class HuyHoaDonDto
    {
        [Required]
        public string MaHD { get; set; } = null!;

        [Required]
        public string MaNV { get; set; } = null!;

        public string? GhiChu { get; set; }

        [Required]
        public List<XuLyThuocTrongHoaDonDto> XuLyThuocs { get; set; } = new List<XuLyThuocTrongHoaDonDto>();
    }

    public class XuLyThuocTrongHoaDonDto
    {
        [Required]
        public string MaLo { get; set; } = null!;

        [Required]
        public string LoaiXuLy { get; set; } = null!; // "HUY" hoặc "HOAN_LAI"

        [Range(0.01, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public decimal? SoLuongHuy { get; set; } // Chỉ cần khi LoaiXuLy = "HUY"

        [StringLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự")]
        public string? LyDoHuy { get; set; } // Chỉ cần khi LoaiXuLy = "HUY"

        public string? GhiChu { get; set; }
    }

    // DTO cho hủy từ kho
    public class HuyTuKhoDto
    {
        [Required]
        public string MaNhanVien { get; set; } = null!;

        [Required]
        [StringLength(1000, ErrorMessage = "Lý do hủy không được vượt quá 1000 ký tự")]
        public string LyDoHuy { get; set; } = null!;

        [Required]
        public List<ChiTietHuyTuKhoDto> ChiTietPhieuHuy { get; set; } = new List<ChiTietHuyTuKhoDto>();
    }

    public class ChiTietHuyTuKhoDto
    {
        [Required]
        public string MaLo { get; set; } = null!;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số lượng hủy phải lớn hơn 0")]
        public decimal SoLuongHuy { get; set; }

        [StringLength(500, ErrorMessage = "Lý do chi tiết không được vượt quá 500 ký tự")]
        public string? LyDoChiTiet { get; set; }
    }

    // DTO cho hủy từ hóa đơn
    public class HuyTuHoaDonDto
    {
        [Required]
        public string MaHoaDon { get; set; } = null!;

        [Required]
        public string MaNhanVien { get; set; } = null!;

        [StringLength(1000, ErrorMessage = "Lý do hủy không được vượt quá 1000 ký tự")]
        public string? LyDoHuy { get; set; }

        [Required]
        public List<ChiTietXuLyHoaDonDto> ChiTietXuLy { get; set; } = new List<ChiTietXuLyHoaDonDto>();
    }

    public class ChiTietXuLyHoaDonDto
    {
        [Required]
        public string MaLo { get; set; } = null!;

        [Required]
        public string LoaiXuLy { get; set; } = null!; // "HUY" hoặc "HOAN_LAI"

        [Range(0.01, double.MaxValue, ErrorMessage = "Số lượng hủy phải lớn hơn 0")]
        public decimal? SoLuongHuy { get; set; } // Chỉ cần khi LoaiXuLy = "HUY"

        [StringLength(500, ErrorMessage = "Lý do chi tiết không được vượt quá 500 ký tự")]
        public string? LyDoChiTiet { get; set; } // Lý do cho việc hủy hoặc hoàn lại

        public string? GhiChu { get; set; }
    }
}