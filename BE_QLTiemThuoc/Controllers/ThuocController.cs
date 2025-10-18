using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThuocController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ThuocController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Thuoc/TopLoaiThuoc
        [HttpGet("TopLoaiThuoc")]
        public async Task<IActionResult> GetTopLoaiThuoc()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                // 1. Thống kê số lượng thuốc theo mã loại
                var thuocGroup = await _context.Thuoc
                    .GroupBy(t => t.MaLoaiThuoc)
                    .Select(g => new
                    {
                        MaLoaiThuoc = g.Key,
                        SoLuongThuoc = g.Count()
                    })
                    .ToListAsync();

                // 2. Lấy toàn bộ loại thuốc
                var loaiThuocList = await _context.LoaiThuoc.ToListAsync();

                // 3. Join thủ công để đảm bảo loại nào cũng có
                var thongKeList = loaiThuocList
                    .Select(loai =>
                    {
                        var thuocInfo = thuocGroup.FirstOrDefault(x => x.MaLoaiThuoc == loai.MaLoaiThuoc);
                        return new LoaiThuocThongKe
                        {
                            MaLoaiThuoc = loai.MaLoaiThuoc,
                            TenLoaiThuoc = loai.TenLoaiThuoc,
                            Icon = loai.Icon,
                            SoLuongThuoc = thuocInfo?.SoLuongThuoc ?? 0 // nếu không có thuốc thì 0
                        };
                    })
                    .OrderByDescending(x => x.SoLuongThuoc)
                    .ToList();

                return thongKeList;
            });

            return Ok(response);
        }


        // GET: api/Thuoc/LoaiThuoc
        [HttpGet("LoaiThuoc")]
        public async Task<IActionResult> GetLoaiThuoc()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _context.LoaiThuoc.ToListAsync();
                return result;
            });

            return Ok(response);
        }

        // GET: api/Thuoc
        [HttpGet]
        public async Task<IActionResult> GetThuoc()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _context.Thuoc
                    .Select(t => new
                    {
                        t.MaThuoc,
                        t.MaLoaiThuoc,
                        t.TenThuoc,
                        t.MoTa,
                        t.UrlAnh,
                        t.DonGiaSi
                    })
                    .ToListAsync();

                return result;
            });

            return Ok(response);
        }

        // GET: api/Thuoc/LoaiDonVi
        [HttpGet("LoaiDonVi")]
        public async Task<IActionResult> GetLoaiDonVi()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var list = await _context.LoaiDonVi.ToListAsync();
                return list;
            });

            return Ok(response);
        }

        // GET: api/ListThuocDetail
        [HttpGet("ListThuocDetail")]
        public async Task<IActionResult> GetListThuocDetail()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var result = await _context.Thuoc
                    .Select(t => new {
                        t.MaThuoc,
                        t.MaLoaiThuoc,
                        t.TenThuoc,
                        t.ThanhPhan,
                        t.MoTa,
                        t.MaLoaiDonVi,
                        t.SoLuong,
                        t.CongDung,
                        t.CachDung,
                        t.LuuY,
                        t.UrlAnh,
                        t.MaNCC,
                        t.DonGiaSi,
                        t.DonGiaLe,
                        TenNCC = _context.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                        TenLoaiDonVi = _context.Set<LoaiDonVi>().Where(d => d.MaLoaiDonVi == t.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault()
                    })
                    .ToListAsync();
                return result;
            });

            return Ok(response);
        }
        // GET: api/Thuoc/ByLoai/{maLoaiThuoc}
        [HttpGet("ByLoai/{maLoaiThuoc}")]
        public async Task<IActionResult> GetThuocByLoai(string maLoaiThuoc)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var thuocList = await _context.Thuoc
                    .Where(t => t.MaLoaiThuoc == maLoaiThuoc)
                    .Select(t => new
                    {
                        t.MaThuoc,
                        t.MaLoaiThuoc,
                        t.TenThuoc,
                        t.MoTa,
                        t.UrlAnh,
                        t.DonGiaSi,
                        TenNCC = _context.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                        TenLoaiDonVi = _context.Set<LoaiDonVi>().Where(d => d.MaLoaiDonVi == t.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault()
                    })
                    .ToListAsync();

                return thuocList;
            });

            return Ok(response);
        }

        // GET: api/Thuoc/{maThuoc}
        [HttpGet("{maThuoc}")]
        public async Task<IActionResult> GetThuocById(string maThuoc)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var thuoc = await _context.Thuoc
                    .Where(t => t.MaThuoc == maThuoc)
                    .Select(t => new {
                        t.MaThuoc,
                        t.MaLoaiThuoc,
                        t.TenThuoc,
                        t.ThanhPhan,
                        t.MoTa,
                        t.MaLoaiDonVi,
                        t.SoLuong,
                        t.CongDung,
                        t.CachDung,
                        t.LuuY,
                        t.UrlAnh,
                        t.MaNCC,
                        t.DonGiaSi,
                        t.DonGiaLe,
                        TenNCC = _context.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                        TenLoaiDonVi = _context.Set<LoaiDonVi>().Where(d => d.MaLoaiDonVi == t.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();
                if (thuoc == null) throw new Exception("Không tìm thấy thuốc.");
                return thuoc;
            });

            return Ok(response);
        }

        // POST: api/Thuoc
        [HttpPost]
        public async Task<IActionResult> PostThuoc([FromBody] Thuoc thuoc)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (!ModelState.IsValid)
                    throw new Exception("Dữ liệu không hợp lệ.");

                if (await _context.Thuoc.AnyAsync(t => t.MaThuoc == thuoc.MaThuoc))
                    throw new Exception("Mã thuốc đã tồn tại.");

                _context.Thuoc.Add(thuoc);
                await _context.SaveChangesAsync();

                return thuoc;
            });

            return Ok(response);
        }

        // PUT: api/Thuoc/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutThuoc(string id, [FromBody] Thuoc thuoc)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (id != thuoc.MaThuoc)
                    throw new Exception("Mã thuốc không khớp.");

                var entity = await _context.Thuoc.FindAsync(id);
                if (entity == null)
                    throw new Exception("Không tìm thấy thuốc.");

                // Cập nhật các trường
                _context.Entry(entity).CurrentValues.SetValues(thuoc);
                await _context.SaveChangesAsync();

                return true; // hoặc return entity nếu muốn trả về dữ liệu sau cập nhật
            });

            return Ok(response);
        }

        // DELETE: api/Thuoc/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteThuoc(string id)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var thuoc = await _context.Thuoc.FindAsync(id);
                if (thuoc == null)
                    throw new Exception("Không tìm thấy thuốc.");

                _context.Thuoc.Remove(thuoc);
                await _context.SaveChangesAsync();

                return true;
            });

            return Ok(response);
        }
    }
}
