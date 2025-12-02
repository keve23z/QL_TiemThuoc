using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;
using BE_QLTiemThuoc.Dto;

namespace BE_QLTiemThuoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NhanVienController : ControllerBase
    {
        private readonly NhanVienService _service;

        public NhanVienController(NhanVienService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NhanVien>>> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NhanVien>> GetById(string id)
        {
            var nv = await _service.GetByIdAsync(id);
            if (nv == null)
                return NotFound();
            return Ok(nv);
        }

        [HttpPost]
        public async Task<ActionResult<NhanVien>> Create([FromBody] CreateNhanVienDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var nhanVien = new NhanVien
            {
                MaNV = dto.MaNV,
                HoTen = dto.HoTen,
                NgaySinh = dto.NgaySinh,
                GioiTinh = dto.GioiTinh,
                DiaChi = dto.DiaChi,
                DienThoai = dto.DienThoai,
                ChucVu = dto.ChucVu
            };

            await _service.CreateAsync(nhanVien);
            return CreatedAtAction(nameof(GetById), new { id = nhanVien.MaNV }, nhanVien);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<NhanVien>> Update(string id, [FromBody] UpdateNhanVienDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var nhanVien = await _service.GetByIdAsync(id);
            if (nhanVien == null)
                return NotFound();

            nhanVien.HoTen = dto.HoTen;
            nhanVien.NgaySinh = dto.NgaySinh;
            nhanVien.GioiTinh = dto.GioiTinh;
            nhanVien.DiaChi = dto.DiaChi;
            nhanVien.DienThoai = dto.DienThoai;
            if (dto.ChucVu != null)
                nhanVien.ChucVu = dto.ChucVu;

            await _service.UpdateAsync(nhanVien);
            return Ok(nhanVien);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var nhanVien = await _service.GetByIdAsync(id);
            if (nhanVien == null)
                return NotFound();

            await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}