using System.ComponentModel.DataAnnotations;

namespace BE_QLTiemThuoc.DTOs
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        public string TenDangNhap { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string MatKhau { get; set; }
    }

    public class LoginResponse
    {
        public string Message { get; set; }
        public string MaTK { get; set; }
        public string TenDangNhap { get; set; }
        public string Email { get; set; }
    }
}
