using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Services;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NhomLoaiController : ControllerBase
    {
        private readonly NhomLoaiService _service;

        public NhomLoaiController(NhomLoaiService service)
        {
            _service = service;
        }

        // GET: api/NhomLoai
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var list = await _service.GetAllAsync();
                return list;
            });
            return Ok(response);
        }

        // GET: api/NhomLoai/{maNhom}
        [HttpGet("{maNhom}")]
        public async Task<IActionResult> GetById(string maNhom)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var item = await _service.GetByIdAsync(maNhom);
                return item;
            });
            return Ok(response);
        }

        // GET: api/NhomLoai/Loai/{maNhom}
        [HttpGet("Loai/{maNhom}")]
        public async Task<IActionResult> GetLoaiByNhom(string maNhom)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var list = await _service.GetLoaiByNhomAsync(maNhom);
                return list;
            });
            return Ok(response);
        }

        // GET: api/NhomLoai/WithLoai
        [HttpGet("WithLoai")]
        public async Task<IActionResult> GetGroupsWithLoai()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var groups = await _service.GetGroupsWithLoaiAsync();
                return groups;
            });

            return Ok(response);
        }
    }
}
