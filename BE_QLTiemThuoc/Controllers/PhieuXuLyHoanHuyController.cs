using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Dto;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhieuXuLyHoanHuyController : ControllerBase
    {
        private readonly PhieuXuLyHoanHuyService _service;

        public PhieuXuLyHoanHuyController(PhieuXuLyHoanHuyService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? start, [FromQuery] DateTime? end, [FromQuery] string? maNV_Tao, [FromQuery] bool? loaiNguon, [FromQuery] int? trangThai)
        {
            try
            {
                var list = await _service.GetAllAsync(start, end, maNV_Tao, loaiNguon, trangThai);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("Details/{maPXH}")]
        public async Task<IActionResult> Details(string maPXH)
        {
            if (string.IsNullOrWhiteSpace(maPXH)) return BadRequest("MaPXH is required");
            try
            {
                var details = await _service.GetDetailsAsync(maPXH);
                if (details == null) return NotFound();
                return Ok(details);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost("QuickCreate")]
        public async Task<IActionResult> QuickCreate([FromBody]PhieuXuLyHoanHuyQuickCreateDto dto)
        {
            if (dto == null) return BadRequest(new ApiResponse<string> { Status = -1, Message = "Payload is required", Data = null });

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _service.CreateQuickRequestAsync(dto);
                return result;
            });

            return Ok(response);
        }
        
    }
}
