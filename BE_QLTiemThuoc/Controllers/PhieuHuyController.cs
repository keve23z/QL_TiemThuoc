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
        public async Task<IActionResult> GetByDateRange([FromQuery] string startDate, [FromQuery] string endDate, [FromQuery] int? loaiHuy)
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

            // loaiHuy: 0 => KHO (hủy từ kho), 1 => HOADON (hủy từ hóa đơn). If not provided, return all.
            bool? loaiHuyFlag = null;
            if (loaiHuy.HasValue)
            {
                if (loaiHuy.Value == 0) loaiHuyFlag = false;
                else if (loaiHuy.Value == 1) loaiHuyFlag = true;
                else
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Status = -1,
                        Message = "loaiHuy must be 0 (KHO) or 1 (HOADON)",
                        Data = null
                    });
                }
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var data = await _service.GetByDateRangeAsync(sDate, eDate, loaiHuyFlag);
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

        // GET: api/PhieuHuy/TraceMaLo?maLo={maLo}&depth={depth}
        [HttpGet("TraceMaLo")]
        public async Task<IActionResult> TraceMaLo([FromQuery] string maLo, [FromQuery] int? depth)
        {
            if (string.IsNullOrWhiteSpace(maLo))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "maLo is required",
                    Data = null
                });
            }

            var maxDepth = depth ?? 2;

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var data = await _service.FindOriginalPhieuNhapsByMaLoAsync(maLo, maxDepth);
                return data;
            });

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

            // Validate dựa trên loại hủy: 0 = KHO, 1 = HOA_DON
            if (dto.LoaiHuy == 0)
            {
                if (dto.HuyTuKho == null || dto.HuyTuKho.ChiTietPhieuHuy == null || !dto.HuyTuKho.ChiTietPhieuHuy.Any())
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Status = -1,
                        Message = "ChiTietPhieuHuy cannot be empty for KHO (LoaiHuy=0).",
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
            else if (dto.LoaiHuy == 1)
            {
                if (dto.HuyTuHoaDon == null || dto.HuyTuHoaDon.ChiTietXuLy == null || !dto.HuyTuHoaDon.ChiTietXuLy.Any())
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Status = -1,
                        Message = "ChiTietXuLy cannot be empty for HOADON (LoaiHuy=1).",
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
                    Message = "LoaiHuy must be 0 (KHO) or 1 (HOADON).",
                    Data = null
                });
            }
        }

        // PUT: api/PhieuHuy/update/{maPH}
        [HttpPut("update/{maPH}")]
        public async Task<IActionResult> UpdatePhieuHuy(string maPH, [FromBody] PhieuHuyDto dto)
        {
            if (string.IsNullOrEmpty(maPH))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "MaPH is required",
                    Data = null
                });
            }

            if (dto == null || dto.ChiTietPhieuHuys == null || !dto.ChiTietPhieuHuys.Any())
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid PhieuHuy data or empty ChiTietPhieuHuys",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _service.UpdatePhieuHuyAsync(maPH, dto);
                return result;
            });

            return Ok(response);
        }

        // DELETE: api/PhieuHuy/delete/{maPH}
        [HttpDelete("delete/{maPH}")]
        public async Task<IActionResult> DeletePhieuHuy(string maPH)
        {
            if (string.IsNullOrEmpty(maPH))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "MaPH is required",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _service.DeletePhieuHuyAsync(maPH);
                return result;
            });

            return Ok(response);
        }
    }
}