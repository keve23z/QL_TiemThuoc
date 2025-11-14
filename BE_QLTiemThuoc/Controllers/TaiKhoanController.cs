using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.DTOs;

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
            return Ok(new CheckUsernameResponse { Exists = exists });
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] RegisterRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kiểm tra tên đăng nhập đã tồn tại
                var existingUser = await _context.TaiKhoans
                    .FirstOrDefaultAsync(u => u.TenDangNhap == request.TenDangNhap);
                if (existingUser != null)
                {
                    return BadRequest("Tên đăng nhập đã tồn tại.");
                }

                // Sinh token xác thực email
                var tokenBytes = new byte[32];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(tokenBytes);
                }
                string emailToken = Convert.ToBase64String(tokenBytes);

                // Tạo đối tượng TaiKhoan từ DTO
                var newAccount = new TaiKhoan
                {
                    MaTK = GenerateAccountCode(),
                    TenDangNhap = request.TenDangNhap,
                    MatKhau = request.MatKhau,
                    EMAIL = request.Email,
                    ISEMAILCONFIRMED = 0,
                    EMAILCONFIRMATIONTOKEN = emailToken,
                    OTP = null,
                    KhachHang = null
                };

                _context.TaiKhoans.Add(newAccount);
                await _context.SaveChangesAsync();

                // Gửi email xác thực
                string confirmationLink = $"https://localhost:7283/api/TaiKhoan/ConfirmEmail?token={Uri.EscapeDataString(emailToken)}";
                await SendConfirmationEmail(newAccount.EMAIL, confirmationLink);

                return Ok(new RegisterResponse { Message = "Tạo tài khoản thành công. Vui lòng kiểm tra email để xác thực." });
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
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(u => u.TenDangNhap == request.TenDangNhap && u.MatKhau == request.MatKhau);

            if (user == null)
                return Unauthorized("Sai tên đăng nhập hoặc mật khẩu.");

            if (user.ISEMAILCONFIRMED == 0)
                return BadRequest("Tài khoản chưa xác thực email.");

            return Ok(new LoginResponse
            {
                Message = "Đăng nhập thành công",
                MaTK = user.MaTK,
                TenDangNhap = user.TenDangNhap,
                Email = user.EMAIL
            });
        }

        // Gửi OTP về email khi quên mật khẩu
        [HttpPost("SendOtp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(u => u.TenDangNhap == request.TenDangNhap && u.EMAIL == request.Email);

            if (user == null)
                return NotFound("Không tìm thấy tài khoản với tên đăng nhập và email này.");

            // Sinh OTP ngẫu nhiên 6 số
            var rng = new Random();
            int otp = rng.Next(100000, 999999);

            user.OTP = otp;
            await _context.SaveChangesAsync();

            // Gửi OTP về email
            var smtp = new SmtpClient("smtp.gmail.com")
            {
                Credentials = new NetworkCredential("chaytue0203@gmail.com", "kctw ltds teaj luvb"),
                EnableSsl = true,
                Port = 587
            };
            
            var mail = new MailMessage("khangtuong040@gmail.com", user.EMAIL)
            {
                Subject = "Mã OTP đặt lại mật khẩu - Medion",
                Body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <h2 style='color: #17a2b8;'>Đặt lại mật khẩu</h2>
                        <p>Xin chào <strong>{user.TenDangNhap}</strong>,</p>
                        <p>Bạn đã yêu cầu đặt lại mật khẩu. Mã OTP của bạn là:</p>
                        <div style='background: #f8f9fa; padding: 20px; text-align: center; margin: 20px 0;'>
                            <h1 style='color: #17a2b8; margin: 0; font-size: 36px; letter-spacing: 5px;'>{otp}</h1>
                        </div>
                        <p>Mã OTP có hiệu lực trong 5 phút.</p>
                        <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #ddd;'>
                        <p style='color: #6c757d; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời.</p>
                    </div>
                ",
                IsBodyHtml = true
            };
            
            await smtp.SendMailAsync(mail);

            return Ok(new SendOtpResponse { Message = "OTP đã được gửi về email của bạn." });
        }

        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(u => u.TenDangNhap == request.TenDangNhap && u.EMAIL == request.Email);

            if (user == null)
                return NotFound("Không tìm thấy tài khoản.");

            if (user.OTP == null || user.OTP != request.Otp)
                return BadRequest("OTP không đúng hoặc đã hết hạn.");

            user.MatKhau = request.MatKhauMoi;
            user.OTP = null; // Xóa OTP sau khi đổi mật khẩu thành công
            await _context.SaveChangesAsync();

            return Ok(new ResetPasswordResponse { Message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
        }
    }
}