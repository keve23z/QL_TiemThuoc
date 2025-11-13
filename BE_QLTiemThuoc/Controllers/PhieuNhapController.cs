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

        // GET: api/PhieuNhap/GetByDateRange?startDate=2025-01-01&endDate=2025-12-31&maNV=NV001&maNCC=NCC001
        [HttpGet("GetByDateRange")]
        public async Task<IActionResult> GetByDateRange([FromQuery] string startDate, [FromQuery] string endDate, [FromQuery] string? maNV = null, [FromQuery] string? maNCC = null)
        {
            DateTime sDate, eDate;
            if (!TryParseDate(startDate, out sDate))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "startDate must be a valid date. Accepted formats: yyyy-MM-dd, yyyy-MM-ddTHH:mm:ss, dd/MM/yyyy",
                    Data = null
                });
            }
            if (!TryParseDate(endDate, out eDate))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "endDate must be a valid date. Accepted formats: yyyy-MM-dd, yyyy-MM-ddTHH:mm:ss, dd/MM/yyyy",
                    Data = null
                });
            }

            if (sDate > eDate)
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
                var data = await _service.GetByDateRangeAsync(sDate, eDate, maNV, maNCC);
                return data;
            });

            return Ok(response);
        }

        private static bool TryParseDate(string? input, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(input)) return false;
            // Try ISO and yyyy-MM-dd
            if (DateTime.TryParse(input, null, System.Globalization.DateTimeStyles.AssumeLocal, out result)) return true;
            var formats = new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy" };
            return DateTime.TryParseExact(input, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result);
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
                var result = await _service.AddPhieuNhapAsync(phieuNhapDto);
                return result;
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