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

        // POST: api/PhieuHuy/huy-thuoc - API thống nhất cho tất cả loại hủy
        [HttpPost("huy-thuoc")]
        public async Task<IActionResult> HuyThuoc([FromBody] HuyThuocRequestDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid input data.",
                    Data = null
                });
            }

            // Validate dựa trên loại hủy
            if (dto.LoaiHuy == "TU_KHO")
            {
                if (dto.HuyTuKho == null || dto.HuyTuKho.ChiTietPhieuHuy == null || !dto.HuyTuKho.ChiTietPhieuHuy.Any())
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Status = -1,
                        Message = "ChiTietPhieuHuy cannot be empty for TU_KHO.",
                        Data = null
                    });
                }

                var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
                {
                    var result = await _service.HuyTuKhoAsync(dto.HuyTuKho);
                    return result;
                });

                return Ok(response);
            }
            else if (dto.LoaiHuy == "TU_HOA_DON")
            {
                if (dto.HuyTuHoaDon == null || dto.HuyTuHoaDon.ChiTietXuLy == null || !dto.HuyTuHoaDon.ChiTietXuLy.Any())
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Status = -1,
                        Message = "ChiTietXuLy cannot be empty for TU_HOA_DON.",
                        Data = null
                    });
                }

                var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
                {
                    var result = await _service.HuyTuHoaDonAsync(dto.HuyTuHoaDon);
                    return result;
                });

                return Ok(response);
            }
            else
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "LoaiHuy must be 'TU_KHO' or 'TU_HOA_DON'.",
                    Data = null
                });
            }
        }
    }
}