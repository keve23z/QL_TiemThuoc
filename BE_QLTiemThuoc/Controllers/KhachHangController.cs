using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;

namespace BE_QLTiemThuoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KhachHangController : ControllerBase
    {
        private readonly KhachHangService _service;

        public KhachHangController(KhachHangService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<KhachHang>>> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }

        // GET: api/KhachHang/{maKhachHang}
        [HttpGet("{maKhachHang}")]
        public async Task<ActionResult<KhachHang>> GetById(string maKhachHang)
        {
            if (string.IsNullOrWhiteSpace(maKhachHang)) return BadRequest("maKhachHang is required");

            var kh = await _service.GetByIdAsync(maKhachHang);
            if (kh == null) return NotFound("Không tìm thấy khách hàng.");

            return Ok(kh);
        }


        [HttpPost]
        public async Task<ActionResult<KhachHang>> CreateKhachHang(KhachHang dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll), new { id = created.MAKH }, created);
        }
        //[HttpPut("UpdateThongTin/{maKH}")]
        //public async Task<IActionResult> UpdateThongTin(string maKH, [FromBody] KhachHangUpdateDto dto)
        //{
        //    if (string.IsNullOrWhiteSpace(maKH) || dto == null)
        //        return BadRequest("Thông tin không hợp lệ.");

        //    var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.MAKH == maKH);
        //    if (khachHang == null)
        //        return NotFound("Không tìm thấy khách hàng.");

        //    // Cập nhật các thông tin cho phép thay đổi
        //    khachHang.HOTEN = dto.HOTEN ?? khachHang.HOTEN;
        //    khachHang.NGAYSINH = dto.NGAYSINH ?? khachHang.NGAYSINH;
        //    khachHang.SODT = dto.SODT ?? khachHang.SODT;

        //    await _context.SaveChangesAsync();

        //    return Ok(new { message = "Cập nhật thông tin khách hàng thành công." });
        //}


    }

}
