using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThuocViewController : ControllerBase
    {
        private readonly ThuocViewService _service;

        public ThuocViewController(ThuocViewService service)
        {
            _service = service;
        }

        // GET: api/ThuocView/ChuaTachLe
        [HttpGet("ChuaTachLe")]
        public async Task<IActionResult> GetChuaTachLe()
        {
            var list = await _service.GetChuaTachLeAsync();
            return Ok(list);
        }

        // GET: api/ThuocView/DaTachLe
        [HttpGet("DaTachLe")]
        public async Task<IActionResult> GetDaTachLe()
        {
            var list = await _service.GetDaTachLeAsync();
            return Ok(list);
        }

        // GET: api/ThuocView/TongSoLuongCon
        [HttpGet("TongSoLuongCon")]
        public async Task<IActionResult> GetTongSoLuongCon()
        {
            var list = await _service.GetTongSoLuongConAsync();
            return Ok(list);
        }
    }
}
