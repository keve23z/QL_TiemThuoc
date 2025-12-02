using System.ComponentModel.DataAnnotations;

namespace BE_QLTiemThuoc.DTOs
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        public string TenDangNhap { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        public string Email { get; set; }

        [Required(ErrorMessage = "OTP không được để trống")]
        public string Otp { get; set; }

        [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
        public string MatKhauMoi { get; set; }
    }

    public class ResetPasswordByAdminRequest
    {
        [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
        public string NewPassword { get; set; }
    }

    public class ResetPasswordResponse
    {
        public string Message { get; set; }
    }
}
