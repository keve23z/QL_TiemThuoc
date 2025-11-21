using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_QLTiemThuoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoaiThuocController : ControllerBase
    {
        private readonly LoaiThuocService _service;
        private readonly NhomLoaiService _nhomService;

        public LoaiThuocController(LoaiThuocService service, NhomLoaiService nhomService)
        {
            _service = service;
            _nhomService = nhomService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }

        [HttpGet("ByNhom/{maNhom}")]
        public async Task<IActionResult> GetByNhom(string maNhom)
        {
            // Validate group exists
            try
            {
                var nhom = await _nhomService.GetByIdAsync(maNhom);
            }
            catch
            {
                return BadRequest(new { error = "NhomLoai not found" });
            }

            var list = await _service.GetByNhomAsync(maNhom);
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] LoaiThuoc dto)
        {
            var (ok, err) = await _service.CreateAsync(dto);
            if (!ok) return BadRequest(new { error = err });
            return Created(string.Empty, dto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] LoaiThuoc dto)
        {
            var (ok, err) = await _service.UpdateAsync(id, dto);
            if (!ok) return BadRequest(new { error = err });
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var (ok, err) = await _service.DeleteAsync(id);
            if (!ok) return BadRequest(new { error = err });
            return NoContent();
        }
    }
}
