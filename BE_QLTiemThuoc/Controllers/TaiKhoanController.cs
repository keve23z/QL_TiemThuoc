using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Identity.Data;
using BE_QLTiemThuoc.Model;

namespace BE_QLTiemThuoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TaiKhoanController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TaiKhoanController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaiKhoan>>> GetAll()
        {
            return await _context.TaiKhoans.ToListAsync();
        }

        [HttpGet("CheckUsername")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Thiếu tên đăng nhập.");

            var exists = await _context.TaiKhoans.AnyAsync(u => u.TenDangNhap == username);
            // Trả về true nếu đã tồn tại, false nếu chưa
            return Ok(exists);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] TaiKhoan model)
        {
            try
            {
                if (model == null ||
                    string.IsNullOrWhiteSpace(model.TenDangNhap) ||
                    string.IsNullOrWhiteSpace(model.MatKhau) ||
                    string.IsNullOrWhiteSpace(model.EMAIL))
                {
                    return BadRequest("Thông tin tài khoản không hợp lệ.");
                }

                // Kiểm tra tên đăng nhập đã tồn tại
                var existingUser = await _context.TaiKhoans
                    .FirstOrDefaultAsync(u => u.TenDangNhap == model.TenDangNhap);
                if (existingUser != null)
                {
                    return BadRequest("Tên đăng nhập đã tồn tại.");
                }

                // Tạo mã tài khoản tự động
                model.MaTK = GenerateAccountCode();

                // Sinh token xác thực email
                var tokenBytes = new byte[32];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(tokenBytes);
                }
                string emailToken = Convert.ToBase64String(tokenBytes);

                // Thiết lập mặc định
                model.ISEMAILCONFIRMED = 0;
                model.EMAILCONFIRMATIONTOKEN = emailToken;
                model.OTP = null;
                model.KhachHang = null;

                _context.TaiKhoans.Add(model);
                await _context.SaveChangesAsync();

                // Gửi email xác thực
                string confirmationLink = $"https://localhost:7283/api/TaiKhoan/ConfirmEmail?token={Uri.EscapeDataString(emailToken)}";
                await SendConfirmationEmail(model.EMAIL, confirmationLink);

                return Ok(new { message = "Tạo tài khoản thành công. Vui lòng kiểm tra email để xác thực." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi phía server.",
                    error = ex.Message
                });
            }
        }
        private async Task SendConfirmationEmail(string toEmail, string confirmationLink)
        {
            var smtp = new SmtpClient("smtp.gmail.com") // Thêm host ở đây
            {
                Credentials = new NetworkCredential("chaytue0203@gmail.com", "kctw ltds teaj luvb"),
                EnableSsl = true,
                Port = 587
            };
            var mail = new MailMessage("khangtuong040@gmail.com", toEmail)
            {
                Subject = "Xác thực tài khoản",
                Body = $"Vui lòng xác thực tài khoản bằng cách click vào link: {confirmationLink}"
            };
            await smtp.SendMailAsync(mail);
        }
        [HttpGet("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
                return Content("<h2 style='color:#03A9F4;text-align:center;margin-top:40px'>Token không hợp lệ.</h2>", "text/html; charset=utf-8");

            var user = await _context.TaiKhoans.FirstOrDefaultAsync(u => u.EMAILCONFIRMATIONTOKEN == token);
            if (user == null)
                return Content("<h2 style='color:#03A9F4;text-align:center;margin-top:40px'>Token không hợp lệ hoặc đã xác thực.</h2>", "text/html; charset=utf-8");

            user.ISEMAILCONFIRMED = 1;
            user.EMAILCONFIRMATIONTOKEN = null;
            await _context.SaveChangesAsync();

            string html = @"
<body style='background:#f5f6fa;'>
  <div style='max-width:400px;margin:60px auto;padding:32px 24px;background:#fff;border-radius:12px;box-shadow:0 2px 12px #0001;text-align:center'>
    <svg width='60' height='60' viewBox='0 0 24 24' fill='none' style='margin-bottom:16px'>
      <circle cx='12' cy='12' r='12' fill='#03A9F4'/>
      <path d='M7 13l3 3 7-7' stroke='#fff' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/>
    </svg>
    <h2 style='color:#03A9F4'>Đã xác thực email thành công!</h2>
  </div>
</body>
";
            return Content(html, "text/html; charset=utf-8");
        }
        private string GenerateAccountCode()
        {
            var lastAccount = _context.TaiKhoans
                .OrderByDescending(t => t.MaTK)
                .FirstOrDefault();

            string lastCode = lastAccount?.MaTK ?? "TK0000";
            int number = int.Parse(lastCode.Substring(2)) + 1;
            return "TK" + number.ToString("D4");
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] TaiKhoan request)
        {
            if (string.IsNullOrWhiteSpace(request.MatKhau) || string.IsNullOrWhiteSpace(request.MatKhau))
                return BadRequest("Thiếu thông tin đăng nhập.");

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(u => u.TenDangNhap == request.TenDangNhap && u.MatKhau == request.MatKhau);

            if (user == null)
                return Unauthorized("Sai tên đăng nhập hoặc mật khẩu.");

            if (user.ISEMAILCONFIRMED == 0)
                return BadRequest("Tài khoản chưa xác thực email.");

            return Ok(new
            {
                message = "Đăng nhập thành công",
                user.MaTK,
                user.TenDangNhap,
                user.EMAIL
            });
        }

        // Gửi OTP về email khi quên mật khẩu
        //[HttpPost("SendOtp")]
        //public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest data)
        //{
        //    string username = data?.Username;
        //    if (string.IsNullOrWhiteSpace(username))
        //        return BadRequest("Thiếu tên đăng nhập.");

        //    var user = await _context.TaiKhoans.FirstOrDefaultAsync(u => u.TenDangNhap == username);
        //    if (user == null)
        //        return NotFound("Không tìm thấy tài khoản.");

        //    // Sinh OTP ngẫu nhiên 6 số
        //    var rng = new Random();
        //    int otp = rng.Next(100000, 999999);

        //    user.OTP = otp;
        //    await _context.SaveChangesAsync();

        //    // Gửi OTP về email
        //    var smtp = new SmtpClient("smtp.gmail.com")
        //    {
        //        Credentials = new NetworkCredential("chaytue0203@gmail.com", "kctw ltds teaj luvb"),
        //        EnableSsl = true,
        //        Port = 587
        //    };
        //    var mail = new MailMessage("khangtuong040@gmail.com", user.EMAIL)
        //    {
        //        Subject = "OTP đặt lại mật khẩu",
        //        Body = $"Mã OTP đặt lại mật khẩu của bạn là: {otp}"
        //    };
        //    await smtp.SendMailAsync(mail);

        //    return Ok(new { message = "OTP đã được gửi về email." });
        //}



        //[HttpPost("ResetPassword")]
        //public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest data)
        //{
        //    string username = data?.Username;
        //    string newPassword = data?.NewPassword;
        //    int otp = data.Otp;

        //    if (string.IsNullOrWhiteSpace(username) ||
        //        string.IsNullOrWhiteSpace(newPassword) ||
        //        otp == 0)
        //    {
        //        return BadRequest("Thiếu thông tin.");
        //    }

        //    var user = await _context.TaiKhoans.FirstOrDefaultAsync(u => u.TenDangNhap == username);
        //    if (user == null)
        //        return NotFound("Không tìm thấy tài khoản.");

        //    if (user.OTP != otp)
        //        return BadRequest("OTP không đúng hoặc đã hết hạn.");

        //    user.MatKhau = newPassword;
        //    user.OTP = null;
        //    await _context.SaveChangesAsync();

        //    return Ok(new { message = "Đổi mật khẩu thành công." });
        //}
    }
}
