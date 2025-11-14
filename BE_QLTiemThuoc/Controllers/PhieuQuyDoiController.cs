using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhieuQuyDoiController : ControllerBase
    {
        private readonly PhieuQuyDoiService _service;

        public PhieuQuyDoiController(PhieuQuyDoiService service)
        {
            _service = service;
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] PhieuQuyDoiBatchCreateDto dto)
        {
            if (dto == null || dto.Items == null || !dto.Items.Any())
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid input. Required: Items (list) with MaThuoc and SoLuongGoc.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var (maPhieu, maLoMoiList) = await _service.CreatePhieuQuyDoiBatchAsync(dto);
                return new { MaPhieuQD = maPhieu, MaLoMoi = maLoMoiList };
            });

            return Ok(response);
        }

        [HttpPost("QuickByMa")]
        public async Task<IActionResult> QuickByMa([FromBody] Dto.PhieuQuyDoiQuickByMaDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.MaThuoc))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid input. MaThuoc is required.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _service.QuickConvertByMaAsync(dto.MaThuoc!, null, dto.MaLoaiDonViMoi, dto.HanSuDungMoi);
                var maPhieu = result.Item1;
                var maLoMoi = result.Item2;
                return new { MaPhieuQD = maPhieu, MaLoMoi = maLoMoi };
            });

            return Ok(response);
        }
    }
}
