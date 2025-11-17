using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;
using BE_QLTiemThuoc.Dto;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhieuHuyController : ControllerBase
    {
        private readonly PhieuHuyService _service;

        public PhieuHuyController(PhieuHuyService service)
        {
            _service = service;
        }

        // GET: api/PhieuHuy/GetByDateRange?startDate=2025-01-01&endDate=2025-12-31
        [HttpGet("GetByDateRange")]
        public async Task<IActionResult> GetByDateRange([FromQuery] string startDate, [FromQuery] string endDate)
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
                var data = await _service.GetByDateRangeAsync(sDate, eDate);
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

        // POST: api/PhieuHuy/CreatePhieuHuy
        [HttpPost("CreatePhieuHuy")]
        public async Task<IActionResult> CreatePhieuHuy([FromBody] PhieuHuyDto phieuHuyDto)
        {
            if (phieuHuyDto == null || phieuHuyDto.ChiTietPhieuHuys == null || !phieuHuyDto.ChiTietPhieuHuys.Any())
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid input data or empty ChiTietPhieuHuys.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _service.CreatePhieuHuyAsync(phieuHuyDto);
                return result;
            });

            return Ok(response);
        }

        // POST: api/PhieuHuy/HuyHoaDon
        [HttpPost("HuyHoaDon")]
        public async Task<IActionResult> HuyHoaDon([FromBody] HuyHoaDonDto huyHoaDonDto)
        {
            if (huyHoaDonDto == null || huyHoaDonDto.XuLyThuocs == null || !huyHoaDonDto.XuLyThuocs.Any())
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid input data or empty XuLyThuocs.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _service.HuyHoaDonAsync(huyHoaDonDto);
                return result;
            });

            return Ok(response);
        }

        // GET: api/PhieuHuy/GetChiTietPhieuHuy/{maPH}
        [HttpGet("GetChiTietPhieuHuy/{maPH}")]
        public async Task<IActionResult> GetChiTietPhieuHuy(string maPH)
        {
            if (string.IsNullOrEmpty(maPH))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "MaPH cannot be null or empty.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var data = await _service.GetChiTietPhieuHuyAsync(maPH);
                return data;
            });

            if (response.Data == null)
            {
                return NotFound(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "PhieuHuy not found.",
                    Data = null
                });
            }

            return Ok(response);
        }
    }
}