using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_QLTiemThuoc.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class BinhLuanController : ControllerBase
 {
 private readonly BinhLuanService _service;
 public BinhLuanController(BinhLuanService service){ _service = service; }

 [HttpGet("{maBL}")]
 public async Task<IActionResult> Get(string maBL) => Ok(await ApiResponseHelper.ExecuteSafetyAsync(()=> _service.GetAsync(maBL)));

 [HttpGet("thuoc/{maThuoc}")]
 public async Task<IActionResult> GetByThuoc(string maThuoc) => Ok(await ApiResponseHelper.ExecuteSafetyAsync(()=> _service.GetByThuocAsync(maThuoc)));

 [HttpPost]
 public async Task<IActionResult> Create([FromBody] BinhLuanCreateDto dto) => Ok(await ApiResponseHelper.ExecuteSafetyAsync(()=> _service.CreateAsync(dto)));

 [HttpDelete("{maBL}")]
 public async Task<IActionResult> Delete(string maBL) => Ok(await ApiResponseHelper.ExecuteSafetyAsync(()=> _service.DeleteAsync(maBL)));
 }
}
