using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KhachHangController : ControllerBase
    {
        private readonly AppDbContext _context;

        public KhachHangController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<KhachHang>>> GetAll()
        {
            return await _context.KhachHangs.ToListAsync();
        }



        [HttpPost]
        public async Task<ActionResult<KhachHang>> CreateKhachHang(KhachHang dto)
        {
            var newMaKH = GenerateAccountCode();

            var kh = new KhachHang
            {
                MAKH = newMaKH,
                HoTen = dto.HoTen,
                NgaySinh = dto.NgaySinh,
                DienThoai = dto.DienThoai,
                GioiTinh = dto.GioiTinh,
                DiaChi = dto.DiaChi
            };

            _context.KhachHangs.Add(kh);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { id = kh.MAKH }, kh);
        }

        // Hàm tạo mã khách hàng tự động
        private string GenerateAccountCode()
        {
            var lastAccount = _context.KhachHangs
                .OrderByDescending(t => t.MAKH)
                .FirstOrDefault();

            string lastCode = lastAccount?.MAKH ?? "KH0000";
            int number = int.Parse(lastCode.Substring(2)) + 1;
            return "KH" + number.ToString("D4");
        }

        // PUT: api/KhachHang/{maKhachHang}
        [HttpPut("{maKhachHang}")]
        public async Task<IActionResult> UpdateKhachHang(string maKhachHang, KhachHang dto)
        {
            if (string.IsNullOrWhiteSpace(maKhachHang)) return BadRequest("maKhachHang is required");

            var updated = await _service.UpdateAsync(maKhachHang, dto);
            if (updated == null) return NotFound("Không tìm thấy khách hàng để cập nhật.");

            return Ok(updated);
        }
    }

}
