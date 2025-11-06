using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Dto;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhieuNhapController : ControllerBase
    {
        private readonly PhieuNhapService _service;

        public PhieuNhapController(PhieuNhapService service)
        {
            _service = service;
        }

        // GET: api/PhieuNhap/GetByDateRange?startDate=2025-01-01&endDate=2025-12-31
        [HttpGet("GetByDateRange")]
        public async Task<IActionResult> GetByDateRange(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Start date must be earlier than or equal to the end date.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var data = await _service.GetByDateRangeAsync(startDate, endDate);
                return data;
            });

            return Ok(response);
        }

        
        [HttpPost("AddPhieuNhap")]
        public async Task<IActionResult> AddPhieuNhap([FromBody] PhieuNhapDto phieuNhapDto)
        {
            // LoThuocHSDs is optional: if missing, server will generate lots from ChiTietPhieuNhaps
            if (phieuNhapDto == null || phieuNhapDto.ChiTietPhieuNhaps == null)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid input data.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var maPN = await _service.AddPhieuNhapAsync(phieuNhapDto);
                return maPN;
            });

            return Ok(response);
        }

    [HttpGet("GetChiTietPhieuNhapByMaPN")]
        public async Task<IActionResult> GetChiTietPhieuNhapByMaPN(string maPN)
        {
            if (string.IsNullOrEmpty(maPN))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "MaPN cannot be null or empty.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var data = await _service.GetChiTietPhieuNhapByMaPNAsync(maPN);
                return data;
            });

            return Ok(response);
        }

    }
}