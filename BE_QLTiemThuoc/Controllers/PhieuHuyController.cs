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
    }
}