namespace BE_QLTiemThuoc.Model
{
    public class ResetPasswordRequest
    {
        public string Username { get; set; }
        public string NewPassword { get; set; }
        public int Otp { get; set; }
    }
}
