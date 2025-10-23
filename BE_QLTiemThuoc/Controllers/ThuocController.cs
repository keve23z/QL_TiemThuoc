using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;


namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThuocController : ControllerBase
    {
    private readonly AppDbContext _context;
    private readonly Cloudinary _cloudinary;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public ThuocController(AppDbContext context, Cloudinary cloudinary, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _context = context;
            _cloudinary = cloudinary;
            _env = env;
        }

        // Helper: extract filename from a provided URL or path. Returns null if input empty.
        private static string? ExtractFileNameFromUrl(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            try
            {
                var s = input.Trim();
                // If absolute URL, use LocalPath
                if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(s);
                    s = uri.LocalPath ?? s;
                }
                // strip querystring
                var noQuery = s.Split('?')[0];
                noQuery = noQuery.Trim('/');
                var file = Path.GetFileName(noQuery);
                return string.IsNullOrEmpty(file) ? null : file;
            }
            catch
            {
                try
                {
                    var t = input.Split('?')[0].Trim('/');
                    var f = Path.GetFileName(t);
                    return string.IsNullOrEmpty(f) ? null : f;
                }
                catch
                {
                    return null;
                }
            }
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
        public async Task<IActionResult> PostThuoc([FromForm] ThuocDto thuocDto)
        {
            // Bắt và xử lý ngoại lệ bằng helper tùy chỉnh của bạn
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                // Diagnostic: log incoming form keys and any files for troubleshooting
                try
                {
                    Console.WriteLine($"[PostThuoc] Received DTO: MaThuoc='{thuocDto?.MaThuoc ?? ""}', UrlAnh='{thuocDto?.UrlAnh ?? ""}', FileAnhPresent={(thuocDto?.FileAnh != null ? "yes" : "no")}");
                    if (Request != null && Request.HasFormContentType)
                    {
                        var f = await Request.ReadFormAsync();
                        var keys = f.Keys.Any() ? string.Join(",", f.Keys) : "(none)";
                        Console.WriteLine($"[PostThuoc] RawForm Keys: {keys}");
                        if (f.Files != null && f.Files.Count > 0)
                        {
                            Console.WriteLine($"[PostThuoc] Files: count={f.Files.Count}");
                            for (int i = 0; i < f.Files.Count; i++) Console.WriteLine($"  File[{i}].Name={f.Files[i].Name}, FileName={f.Files[i].FileName}, Length={f.Files[i].Length}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PostThuoc] Could not read Request.Form: " + ex.Message);
                }
                if (!ModelState.IsValid)
                    // Lấy thông báo lỗi chi tiết hơn từ ModelState nếu cần
                    throw new Exception("Dữ liệu không hợp lệ.");
                if (thuocDto == null)
                    throw new Exception("Dữ liệu thuốc (DTO) không được gửi.");

                if (await _context.Thuoc.AnyAsync(t => t.MaThuoc == thuocDto.MaThuoc))
                    throw new Exception("Mã thuốc đã tồn tại.");

                string? imageUrl = null;
                // If model binding didn't populate UrlAnh, try to read it directly from the form collection
                string? providedUrlFromForm = null;
                try
                {
                    if (Request != null && Request.HasFormContentType)
                    {
                        var form = await Request.ReadFormAsync();
                        if (thuocDto != null && string.IsNullOrWhiteSpace(thuocDto.UrlAnh) && form.TryGetValue("UrlAnh", out var v))
                        {
                            providedUrlFromForm = v.ToString();
                            Console.WriteLine($"[PostThuoc] Fallback UrlAnh from Request.Form = '{providedUrlFromForm}'");
                        }
                        // additional fallback: case-insensitive key search
                        if (string.IsNullOrWhiteSpace(providedUrlFromForm))
                        {
                            var foundKey = form.Keys.FirstOrDefault(k => string.Equals(k, "urlanh", StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(foundKey))
                            {
                                providedUrlFromForm = form[foundKey].ToString();
                                Console.WriteLine($"[PostThuoc] Fallback (case-insensitive) UrlAnh from Request.Form key='{foundKey}' = '{providedUrlFromForm}'");
                            }
                        }
                    }
                }
                catch { }

                // KHỞI TẠO UPLOAD PARAMS TRƯỚC VÀ CHỈ ĐỊNH TAGS
                ImageUploadParams? uploadParams = null; // Khai báo ngoài khối if/using

                // --- BƯỚC QUAN TRỌNG: XỬ LÝ ẢNH ---
                // If a file is uploaded (FileAnh), upload to Cloudinary and save the resolved filename.
                // If no file is uploaded but UrlAnh is provided (selecting existing image), just store that filename.
                if (thuocDto != null && thuocDto.FileAnh != null && thuocDto.FileAnh.Length > 0)
                {
                    // Kiểm tra kích thước file để tránh upload quá lớn
                    if (thuocDto.FileAnh.Length > 5242880) // Ví dụ: Giới hạn 5MB
                    {
                        throw new Exception("Kích thước file ảnh không được vượt quá 5MB.");
                    }

                    // 1. Tạo danh sách các tags (đã sửa lỗi)
                    var tagsList = new List<string> { "thuoc" };
                    if (!string.IsNullOrEmpty(thuocDto.MaLoaiThuoc))
                    {
                        tagsList.Add(thuocDto.MaLoaiThuoc.Trim().ToLower());
                    }

                    // 2. Chuyển đổi List<string> thành string, phân cách bằng dấu phẩy
                    string tagsString = string.Join(",", tagsList);

                    using (var stream = thuocDto.FileAnh.OpenReadStream())
                    {
                        // 3. Build a sanitized base name and extension, then ensure uniqueness
                        var originalFileName = Path.GetFileName(thuocDto.FileAnh.FileName);
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName) ?? "file";
                        var ext = Path.GetExtension(originalFileName) ?? ""; // includes leading dot if present

                        // sanitize to Cloudinary-safe chars for public id base
                        string SanitizeForPublicId(string s) => Regex.Replace(s, @"[^A-Za-z0-9_\-]", "_");

                        var baseCandidate = SanitizeForPublicId(nameWithoutExt);

                        // Resolve duplicates against DB by checking UrlAnh (case-insensitive)
                        string candidateStored = baseCandidate + ext; // e.g. myimage.jpg
                        string candidatePublicBase = baseCandidate; // without extension
                        int suffix = 1;
                        while (await _context.Thuoc.AnyAsync(t => t.UrlAnh.ToLower() == candidateStored.ToLower()))
                        {
                            candidatePublicBase = SanitizeForPublicId($"{baseCandidate}_{suffix}");
                            candidateStored = candidatePublicBase + ext;
                            suffix++;
                        }

                        // prepare upload params using the resolved unique public id
                        uploadParams = new ImageUploadParams()
                        {
                            File = new FileDescription(thuocDto.FileAnh.FileName, stream),
                            Folder = "thuoc_images",
                            PublicId = $"thuoc/{candidatePublicBase}",
                            Overwrite = false, // rely on uniqueness to avoid overwrite
                            Tags = tagsString
                        };

                        // Perform upload
                        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                        if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            throw new Exception($"Lỗi Cloudinary: {uploadResult.Error?.Message}");
                        }

                        // Determine extension/format returned by Cloudinary if available
                        string finalFileName;
                        if (!string.IsNullOrEmpty(uploadResult.PublicId))
                        {
                            var lastSegment = uploadResult.PublicId.Contains('/') ? uploadResult.PublicId.Split('/').Last() : uploadResult.PublicId;
                            if (!string.IsNullOrEmpty(uploadResult.Format))
                                finalFileName = lastSegment + "." + uploadResult.Format;
                            else
                                finalFileName = candidateStored; // fallback to candidate we reserved
                        }
                        else if (uploadResult.SecureUrl != null)
                        {
                            finalFileName = Path.GetFileName(new System.Uri(uploadResult.SecureUrl.ToString()).LocalPath);
                        }
                        else
                        {
                            finalFileName = candidateStored;
                        }

                        // Save the resolved filename into DB
                        imageUrl = finalFileName;

                    
                    } // Kết thúc using(stream)
                }
                else
                {
                    // If client provided UrlAnh (existing image name or absolute URL), extract filename when possible
                    var urlToUse = !string.IsNullOrWhiteSpace(thuocDto?.UrlAnh) ? thuocDto.UrlAnh : providedUrlFromForm;
                    if (!string.IsNullOrWhiteSpace(urlToUse))
                    {
                        var extracted = ExtractFileNameFromUrl(urlToUse);
                        imageUrl = extracted ?? urlToUse; // prefer filename, but fallback to raw value
                    }
                }
                // ----------------------------------------------------
                // Debug: show final computed imageUrl that will be saved (if any)
                try
                {
                    Console.WriteLine($"[PostThuoc] Computed imageUrl='{imageUrl}' (providedUrlFromForm='{providedUrlFromForm}')");
                }
                catch { }

                // Ánh xạ các trường từ DTO sang Model chính
                var thuoc = new Thuoc
                {
                    MaThuoc = thuocDto?.MaThuoc ?? string.Empty,
                    MaLoaiThuoc = thuocDto?.MaLoaiThuoc ?? string.Empty,
                    TenThuoc = thuocDto?.TenThuoc ?? string.Empty,
                    ThanhPhan = thuocDto?.ThanhPhan ?? string.Empty,
                    MoTa = thuocDto?.MoTa ?? string.Empty,
                    MaLoaiDonVi = thuocDto?.MaLoaiDonVi ?? string.Empty,
                    SoLuong = thuocDto?.SoLuong ?? 0,
                    CongDung = thuocDto?.CongDung ?? string.Empty,
                    CachDung = thuocDto?.CachDung ?? string.Empty,
                    LuuY = thuocDto?.LuuY ?? string.Empty,
                    MaNCC = thuocDto?.MaNCC ?? string.Empty,
                    DonGiaSi = thuocDto?.DonGiaSi ?? 0,
                    DonGiaLe = thuocDto?.DonGiaLe ?? 0,

                    // LƯU URL TRẢ VỀ TỪ CLOUDINARY VÀO DB (chỉ lưu tên file)
                    UrlAnh = imageUrl ?? string.Empty
                };

                _context.Thuoc.Add(thuoc);
                await _context.SaveChangesAsync();

                // Trả về đối tượng Thuoc đã được thêm
                return thuoc;
            });

            return Ok(response);
        }

        // PUT: api/Thuoc/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutThuoc(string id, [FromForm] Model.Thuoc.ThuocDto thuocDto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                // Debug: log incoming file/url to help diagnose missing UrlAnh updates
                try
                {
                    Console.WriteLine($"[PutThuoc] id={id} FileAnhPresent={(thuocDto?.FileAnh != null ? "yes" : "no")} UrlAnh='{thuocDto?.UrlAnh ?? ""}' MaThuoc='{thuocDto?.MaThuoc ?? ""}'");
                }
                catch { }
                if (thuocDto == null)
                    throw new Exception("Dữ liệu thuốc (DTO) không được gửi.");

                // Diagnostic: log raw form keys and any files sent
                try
                {
                    if (Request != null && Request.HasFormContentType)
                    {
                        var f = await Request.ReadFormAsync();
                        var keys = f.Keys.Any() ? string.Join(",", f.Keys) : "(none)";
                        Console.WriteLine($"[PutThuoc] RawForm Keys: {keys}");
                        if (f.Files != null && f.Files.Count > 0)
                        {
                            Console.WriteLine($"[PutThuoc] Files: count={f.Files.Count}");
                            for (int i = 0; i < f.Files.Count; i++) Console.WriteLine($"  File[{i}].Name={f.Files[i].Name}, FileName={f.Files[i].FileName}, Length={f.Files[i].Length}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PutThuoc] Could not read Request.Form: " + ex.Message);
                }

                if (id != thuocDto.MaThuoc)
                    throw new Exception("Mã thuốc không khớp.");

                var entity = await _context.Thuoc.FindAsync(id);
                if (entity == null)
                    throw new Exception("Không tìm thấy thuốc.");

                // If a new image file is provided, upload it (resolve duplicates same as POST)
                if (thuocDto.FileAnh != null && thuocDto.FileAnh.Length > 0)
                {
                    if (thuocDto.FileAnh.Length > 5242880) // 5MB
                        throw new Exception("Kích thước file ảnh không được vượt quá 5MB.");

                    var tagsList = new List<string> { "thuoc" };
                    if (!string.IsNullOrEmpty(thuocDto.MaLoaiThuoc)) tagsList.Add(thuocDto.MaLoaiThuoc.Trim().ToLower());
                    string tagsString = string.Join(",", tagsList);

                    using (var stream = thuocDto.FileAnh.OpenReadStream())
                    {
                        var originalFileName = Path.GetFileName(thuocDto.FileAnh.FileName);
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName) ?? "file";
                        var ext = Path.GetExtension(originalFileName) ?? "";
                        string SanitizeForPublicId(string s) => Regex.Replace(s, @"[^A-Za-z0-9_\-]", "_");

                        var baseCandidate = SanitizeForPublicId(nameWithoutExt);
                        string candidateStored = baseCandidate + ext;
                        string candidatePublicBase = baseCandidate;
                        int suffix = 1;
                        while (await _context.Thuoc.AnyAsync(t => t.UrlAnh.ToLower() == candidateStored.ToLower()))
                        {
                            candidatePublicBase = SanitizeForPublicId($"{baseCandidate}_{suffix}");
                            candidateStored = candidatePublicBase + ext;
                            suffix++;
                        }

                        var uploadParams = new ImageUploadParams()
                        {
                            File = new FileDescription(thuocDto.FileAnh.FileName, stream),
                            Folder = "thuoc_images",
                            PublicId = $"thuoc/{candidatePublicBase}",
                            Overwrite = false,
                            Tags = tagsString
                        };

                        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                        if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                            throw new Exception($"Lỗi Cloudinary: {uploadResult.Error?.Message}");

                        string finalFileName;
                        if (!string.IsNullOrEmpty(uploadResult.PublicId))
                        {
                            var lastSegment = uploadResult.PublicId.Contains('/') ? uploadResult.PublicId.Split('/').Last() : uploadResult.PublicId;
                            finalFileName = !string.IsNullOrEmpty(uploadResult.Format) ? lastSegment + "." + uploadResult.Format : candidateStored;
                        }
                        else if (uploadResult.SecureUrl != null)
                        {
                            finalFileName = Path.GetFileName(new System.Uri(uploadResult.SecureUrl.ToString()).LocalPath);
                        }
                        else
                        {
                            finalFileName = candidateStored;
                        }

                            // update entity UrlAnh with the newly uploaded filename
                            entity.UrlAnh = finalFileName;

                            // Copy the uploaded image into the FE project's wwwroot so the frontend can serve the file directly
                            try
                            {
                                var beContentRoot = _env?.ContentRootPath;
                                if (!string.IsNullOrEmpty(beContentRoot))
                                {
                                    var solutionDir = Directory.GetParent(beContentRoot)?.FullName;
                                    if (!string.IsNullOrEmpty(solutionDir))
                                    {
                                        var feTargetDir = Path.Combine(solutionDir, "FE_QLTiemThuoc", "wwwroot", "assets_user", "img", "product");
                                        if (!Directory.Exists(feTargetDir)) Directory.CreateDirectory(feTargetDir);
                                        var feTargetPath = Path.Combine(feTargetDir, finalFileName);
                                        try { if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin); } catch { }
                                        using (var fs2 = new FileStream(feTargetPath, FileMode.Create, FileAccess.Write))
                                        {
                                            stream.CopyTo(fs2);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[PutThuoc] Could not save copy into FE webroot: " + ex.Message);
                            }
                    }
                }
                else
                {
                    // No new file uploaded. If client provided a UrlAnh (selecting existing image), update entity accordingly.
                    if (!string.IsNullOrWhiteSpace(thuocDto.UrlAnh))
                    {
                        var extracted = ExtractFileNameFromUrl(thuocDto.UrlAnh);
                        entity.UrlAnh = extracted ?? thuocDto.UrlAnh;
                    }
                    // Otherwise leave entity.UrlAnh unchanged
                }

                // Update other fields from DTO, keeping nullable handling consistent
                entity.MaLoaiThuoc = thuocDto.MaLoaiThuoc ?? entity.MaLoaiThuoc;
                entity.TenThuoc = thuocDto.TenThuoc ?? entity.TenThuoc;
                entity.ThanhPhan = thuocDto.ThanhPhan ?? entity.ThanhPhan;
                entity.MoTa = thuocDto.MoTa ?? entity.MoTa;
                entity.MaLoaiDonVi = thuocDto.MaLoaiDonVi ?? entity.MaLoaiDonVi;
                entity.SoLuong = thuocDto.SoLuong ?? entity.SoLuong;
                entity.CongDung = thuocDto.CongDung ?? entity.CongDung;
                entity.CachDung = thuocDto.CachDung ?? entity.CachDung;
                entity.LuuY = thuocDto.LuuY ?? entity.LuuY;
                entity.MaNCC = thuocDto.MaNCC ?? entity.MaNCC;
                entity.DonGiaSi = thuocDto.DonGiaSi ?? entity.DonGiaSi;
                entity.DonGiaLe = thuocDto.DonGiaLe ?? entity.DonGiaLe;

                // Defensive: if client supplied UrlAnh in the form and no FileAnh was uploaded, ensure UrlAnh is persisted.
                try
                {
                    if ((thuocDto.FileAnh == null || thuocDto.FileAnh.Length == 0) && !string.IsNullOrWhiteSpace(thuocDto.UrlAnh))
                    {
                        var extracted2 = ExtractFileNameFromUrl(thuocDto.UrlAnh);
                        entity.UrlAnh = extracted2 ?? thuocDto.UrlAnh;
                        Console.WriteLine($"[PutThuoc] Applied UrlAnh='{entity.UrlAnh}' to entity {entity.MaThuoc}");
                    }
                }
                catch { }

                await _context.SaveChangesAsync();

                return true;
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
