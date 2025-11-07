using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LieuDungController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public LieuDungController(AppDbContext ctx)
        {
            _ctx = ctx;
        }

        // GET: api/LieuDung
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var list = await _ctx.Set<LieuDung>()
                    .Select(ld => new { ld.MaLD, ld.TenLieuDung })
                    .ToListAsync();
                return list;
            });

            return Ok(response);
        }
    }
}
