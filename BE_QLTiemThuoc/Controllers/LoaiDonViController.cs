using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_QLTiemThuoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoaiDonViController : ControllerBase
    {
        private readonly LoaiDonViService _service;
        public LoaiDonViController(LoaiDonViService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }

        // GetById not required per request; omitted.

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] LoaiDonVi dto)
        {
            var (ok, err) = await _service.CreateAsync(dto);
            if (!ok) return BadRequest(new { error = err });
            return Created(string.Empty, dto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] LoaiDonVi dto)
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
