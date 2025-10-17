using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using System.Threading.Tasks;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NhaCungCapController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NhaCungCapController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/NhaCungCap
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var list = await _context.NhaCungCaps.ToListAsync();
                return list;
            });
            return Ok(response);
        }

        // GET: api/NhaCungCap/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var item = await _context.NhaCungCaps.FirstOrDefaultAsync(n => n.MaNCC == id);
                if (item == null) throw new System.Exception("Không tìm thấy nhà cung cấp.");
                return item;
            });
            return Ok(response);
        }

        // POST: api/NhaCungCap
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NhaCungCap ncc)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (!ModelState.IsValid) throw new System.Exception("Dữ liệu không hợp lệ.");
                if (await _context.NhaCungCaps.AnyAsync(x => x.MaNCC == ncc.MaNCC)) throw new System.Exception("Mã nhà cung cấp đã tồn tại.");
                _context.NhaCungCaps.Add(ncc);
                await _context.SaveChangesAsync();
                return ncc;
            });
            return Ok(response);
        }

        // PUT: api/NhaCungCap/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] NhaCungCap ncc)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (id != ncc.MaNCC) throw new System.Exception("Mã nhà cung cấp không khớp.");
                var entity = await _context.NhaCungCaps.FindAsync(id);
                if (entity == null) throw new System.Exception("Không tìm thấy nhà cung cấp.");
                _context.Entry(entity).CurrentValues.SetValues(ncc);
                await _context.SaveChangesAsync();
                return ncc;
            });
            return Ok(response);
        }
    }
}
