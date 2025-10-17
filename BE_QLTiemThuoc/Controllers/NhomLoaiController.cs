using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using System.Linq;
using System.Threading.Tasks;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NhomLoaiController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NhomLoaiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/NhomLoai
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var list = await _context.NhomLoai.ToListAsync();
                return list;
            });
            return Ok(response);
        }

        // GET: api/NhomLoai/{maNhom}
        [HttpGet("{maNhom}")]
        public async Task<IActionResult> GetById(string maNhom)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var item = await _context.NhomLoai.FindAsync(maNhom);
                if (item == null) throw new System.Exception("Không tìm thấy nhóm loại.");
                return item;
            });
            return Ok(response);
        }

        // GET: api/NhomLoai/Loai/{maNhom}
        [HttpGet("Loai/{maNhom}")]
        public async Task<IActionResult> GetLoaiByNhom(string maNhom)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var list = await _context.LoaiThuoc.Where(l => l.MaNhomLoai == maNhom).ToListAsync();
                return list;
            });
            return Ok(response);
        }

        // GET: api/NhomLoai/WithLoai
        [HttpGet("WithLoai")]
        public async Task<IActionResult> GetGroupsWithLoai()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var groups = await _context.NhomLoai
                    .Select(g => new {
                        g.MaNhomLoai,
                        g.TenNhomLoai,
                        Loai = _context.LoaiThuoc.Where(l => l.MaNhomLoai == g.MaNhomLoai)
                            .Select(l => new { l.MaLoaiThuoc, l.TenLoaiThuoc, l.Icon }).ToList()
                    }).ToListAsync();

                return groups;
            });

            return Ok(response);
        }
    }
}
