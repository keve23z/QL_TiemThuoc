using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
<<<<<<< HEAD
using System.ComponentModel.DataAnnotations;
using BE_QLTiemThuoc.Model.Thuoc;
=======
>>>>>>> ae4a37bae24b21896a21fc63faeed420286e298c

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
                var result = await _context.Thuoc
                    .GroupBy(t => t.MaLoaiThuoc)
                    .Select(g => new
                    {
                        MaLoaiThuoc = g.Key,
                        SoLuongThuoc = g.Count()
                    })
                    .OrderByDescending(x => x.SoLuongThuoc)
                    .Take(6)
                    .ToListAsync();

                var loaiThuocList = await _context.LoaiThuoc.ToListAsync();

                var thongKeList = result
                    .Select(x =>
                    {
                        var loai = loaiThuocList.FirstOrDefault(l => l.MaLoaiThuoc == x.MaLoaiThuoc);
                        return new LoaiThuocThongKe
                        {
                            MaLoaiThuoc = x.MaLoaiThuoc,
                            TenLoaiThuoc = loai?.TenLoaiThuoc ?? "",
                            Icon = loai?.Icon ?? "",
                            SoLuongThuoc = x.SoLuongThuoc
                        };
                    })
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
<<<<<<< HEAD
                    .Select(t => new 
                    {
                        t.MaThuoc,
                        t.MaLoaiThuoc,
                        t.TenThuoc,
                        t.UrlAnh,
                        t.DonGiaSi
                    })
=======
                    //.Include(t => t.LoaiThuoc) // Bật nếu muốn lấy thông tin loại thuốc
>>>>>>> ae4a37bae24b21896a21fc63faeed420286e298c
                    .ToListAsync();
                return result;
            });

            return Ok(response);
        }

<<<<<<< HEAD
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
                        t.UrlAnh,
                        t.DonGiaSi
                    })
                    .ToListAsync();

                return thuocList;
            });

            return Ok(response);
        }

=======
>>>>>>> ae4a37bae24b21896a21fc63faeed420286e298c
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
