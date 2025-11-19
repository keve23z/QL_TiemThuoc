using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;

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
    }
}