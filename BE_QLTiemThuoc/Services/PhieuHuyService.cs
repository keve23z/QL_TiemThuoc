using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Services
{
    public class PhieuHuyService
    {
        private readonly PhieuHuyRepository _repo;

        public PhieuHuyService(PhieuHuyRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<PhieuHuy>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _repo.GetByDateRangeAsync(startDate, endDate);
        }

        public async Task<object> CreatePhieuHuyAsync(PhieuHuyDto dto)
        {
            // Validation
            if (dto.LoaiHuy == "HOADON" && string.IsNullOrEmpty(dto.MaHD))
            {
                throw new ArgumentException("MaHD is required when LoaiHuy is HOADON");
            }

            if (dto.ChiTietPhieuHuys == null || !dto.ChiTietPhieuHuys.Any())
            {
                throw new ArgumentException("ChiTietPhieuHuys cannot be empty");
            }

            // Generate MaPH
            var lastPhieuHuy = await _repo.GetByDateRangeAsync(DateTime.MinValue, DateTime.MaxValue);
            string lastCode = lastPhieuHuy.OrderByDescending(p => p.MaPH).FirstOrDefault()?.MaPH ?? "PH0000000000";
            int number = 1;
            try { number = int.Parse(lastCode.Substring(2)) + 1; } catch { number = 1; }
            var newMaPH = "PH" + number.ToString("D10");

            // Validate tồn kho và tính tổng số lượng
            decimal tongSoLuongHuy = 0;
            foreach (var chiTiet in dto.ChiTietPhieuHuys)
            {
                var tonKho = await _repo.GetTonKhoByMaLoAsync(chiTiet.MaLo);
                if (tonKho == null)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} không tồn tại");
                }

                if (tonKho.SoLuongCon < chiTiet.SoLuongHuy)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} chỉ còn {tonKho.SoLuongCon} không đủ để hủy {chiTiet.SoLuongHuy}");
                }

                tongSoLuongHuy += chiTiet.SoLuongHuy;
            }

            // Tạo phiếu hủy
            var phieuHuy = new PhieuHuy
            {
                MaPH = newMaPH,
                NgayHuy = DateTime.Now,
                MaNV = dto.MaNV,
                LoaiHuy = dto.LoaiHuy,
                MaHD = dto.MaHD,
                TongSoLuongHuy = tongSoLuongHuy,
                GhiChu = dto.GhiChu
            };

            await _repo.AddAsync(phieuHuy);

            // Tạo chi tiết phiếu hủy và cập nhật tồn kho
            var chiTietItems = new List<ChiTietPhieuHuy>();
            foreach (var chiTiet in dto.ChiTietPhieuHuys)
            {
                var chiTietPhieuHuy = new ChiTietPhieuHuy
                {
                    MaPH = newMaPH,
                    MaLo = chiTiet.MaLo,
                    SoLuongHuy = chiTiet.SoLuongHuy,
                    LyDoHuy = chiTiet.LyDoHuy,
                    GhiChu = chiTiet.GhiChu
                };
                chiTietItems.Add(chiTietPhieuHuy);

                // Cập nhật tồn kho
                var tonKho = await _repo.GetTonKhoByMaLoAsync(chiTiet.MaLo);
                if (tonKho != null)
                {
                    tonKho.SoLuongCon -= (int)chiTiet.SoLuongHuy;
                    await _repo.UpdateTonKhoAsync(tonKho);
                }
            }

            await _repo.AddChiTietRangeAsync(chiTietItems);

            // Nếu hủy từ hóa đơn, cập nhật trạng thái hóa đơn
            if (dto.LoaiHuy == "HOADON" && !string.IsNullOrEmpty(dto.MaHD))
            {
                await UpdateHoaDonStatusAfterHuyAsync(dto.MaHD);
            }

            return new { PhieuHuy = phieuHuy, ThuocHoanLai = new List<object>(), Message = "Thành công" };
        }

        private async Task UpdateHoaDonStatusAfterHuyAsync(string maHD)
        {
            var hoaDon = await _repo.GetHoaDonByIdAsync(maHD);
            if (hoaDon == null) return;

            // Kiểm tra xem tất cả chi tiết hóa đơn đã được xử lý chưa
            // Giả sử có phương thức để kiểm tra trạng thái chi tiết
            var allChiTietProcessed = await CheckAllChiTietHoaDonProcessedAsync(maHD);

            if (allChiTietProcessed)
            {
                // Tất cả chi tiết đã xử lý - hoàn tất xử lý hủy
                hoaDon.TrangThaiGiaoHang = -2; // Hoàn tất xử lý hủy
            }
            else
            {
                // Còn chi tiết chưa xử lý hết
                hoaDon.TrangThaiGiaoHang = -3; // Còn chi tiết chưa xử lý hết
            }

            await _repo.UpdateHoaDonAsync(hoaDon);
        }

        private async Task<bool> CheckAllChiTietHoaDonProcessedAsync(string maHD)
        {
            // Lấy tổng số lượng trong hóa đơn
            var hoaDon = await _repo.GetHoaDonByIdAsync(maHD);
            if (hoaDon == null) return false;

            // Lấy tất cả chi tiết hóa đơn (giả sử có thể truy vấn được)
            // Đây là logic giả định - cần điều chỉnh theo cấu trúc thực tế của database

            // Tạm thời: Kiểm tra tổng số lượng đã hủy >= tổng số lượng trong hóa đơn
            // Logic thực tế cần truy vấn ChiTietHoaDon để lấy tổng số lượng gốc

            var tongSoLuongHoaDon = await GetTongSoLuongHoaDonAsync(maHD);
            var tongSoLuongDaHuy = await GetTongSoLuongDaHuyAsync(maHD);

            return tongSoLuongDaHuy >= tongSoLuongHoaDon;
        }

        private async Task<int> GetTongSoLuongHoaDonAsync(string maHD)
        {
            return await _repo.GetTongSoLuongHoaDonAsync(maHD);
        }

        private async Task<int> GetTongSoLuongDaHuyAsync(string maHD)
        {
            // Logic lấy tổng số lượng đã hủy từ các phiếu hủy liên quan
            var phieuHuys = await _repo.GetByDateRangeAsync(DateTime.MinValue, DateTime.MaxValue);
            var relatedPhieuHuys = phieuHuys.Where(p => p.LoaiHuy == "HOADON" && p.MaHD == maHD).ToList();

            return (int)relatedPhieuHuys.Sum(p => p.TongSoLuongHuy);
        }

        public async Task<object> HuyHoaDonAsync(HuyHoaDonDto dto)
        {
            // Validation
            if (string.IsNullOrEmpty(dto.MaHD))
            {
                throw new ArgumentException("MaHD is required");
            }

            if (dto.XuLyThuocs == null || !dto.XuLyThuocs.Any())
            {
                throw new ArgumentException("XuLyThuocs cannot be empty");
            }

            // Kiểm tra hóa đơn tồn tại
            var hoaDon = await _repo.GetHoaDonByIdAsync(dto.MaHD);
            if (hoaDon == null)
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHD} không tồn tại");
            }

            var result = new
            {
                PhieuHuy = (object?)null,
                ThuocHoanLai = new List<object>(),
                Message = ""
            };

            // Tách thuốc cần hủy và thuốc có thể hoàn lại
            var thuocCanHuy = dto.XuLyThuocs.Where(t => t.LoaiXuLy == "HUY").ToList();
            var thuocHoanLai = dto.XuLyThuocs.Where(t => t.LoaiXuLy == "HOAN_LAI").ToList();

            // Xử lý thuốc hoàn lại vào kho
            if (thuocHoanLai.Any())
            {
                foreach (var thuoc in thuocHoanLai)
                {
                    var tonKho = await _repo.GetTonKhoByMaLoAsync(thuoc.MaLo);
                    if (tonKho != null)
                    {
                        // Tăng số lượng tồn kho (giả sử số lượng hoàn lại = số lượng trong chi tiết hóa đơn)
                        // Trong thực tế, cần lấy số lượng từ chi tiết hóa đơn
                        // Ở đây tạm thời giả sử hoàn lại toàn bộ
                        tonKho.SoLuongCon += 1; // Cần logic thực tế để lấy số lượng từ hóa đơn
                        await _repo.UpdateTonKhoAsync(tonKho);
                    }
                }

                result = new
                {
                    PhieuHuy = (object?)null,
                    ThuocHoanLai = thuocHoanLai.Select(t => new { t.MaLo, t.LoaiXuLy, t.GhiChu }).Cast<object>().ToList(),
                    Message = $"Đã hoàn lại {thuocHoanLai.Count} loại thuốc vào kho"
                };
            }

            // Xử lý thuốc cần hủy
            if (thuocCanHuy.Any())
            {
                var phieuHuyDto = new PhieuHuyDto
                {
                    MaNV = dto.MaNV,
                    LoaiHuy = "HOADON",
                    MaHD = dto.MaHD,
                    GhiChu = dto.GhiChu,
                    ChiTietPhieuHuys = thuocCanHuy.Select(t => new ChiTietPhieuHuyDto
                    {
                        MaLo = t.MaLo,
                        SoLuongHuy = t.SoLuongHuy ?? 0,
                        LyDoHuy = t.LyDoHuy ?? "Hủy từ hóa đơn",
                        GhiChu = t.GhiChu
                    }).ToList()
                };

                var phieuHuyResult = await CreatePhieuHuyAsync(phieuHuyDto);

                result = new
                {
                    PhieuHuy = phieuHuyResult,
                    ThuocHoanLai = thuocHoanLai.Select(t => new { t.MaLo, t.LoaiXuLy, t.GhiChu }).Cast<object>().ToList(),
                    Message = $"Đã tạo phiếu hủy cho {thuocCanHuy.Count} loại thuốc và hoàn lại {thuocHoanLai.Count} loại thuốc vào kho"
                };
            }

            // Cập nhật trạng thái hóa đơn
            if (hoaDon != null)
            {
                // Có thể thêm trạng thái mới như "DA_HUY_XU_LY"
                // hoaDon.TrangThai = "DA_HUY_XU_LY";
                // await _repo.UpdateHoaDonAsync(hoaDon);
            }

            return result;
        }

        public async Task<object?> GetChiTietPhieuHuyAsync(string maPH)
        {
            var phieuHuy = await _repo.GetByIdAsync(maPH);
            if (phieuHuy == null) return null;

            // Lấy thông tin chi tiết với join
            var chiTiet = phieuHuy.ChiTietPhieuHuys.Select(ct => new
            {
                ct.MaLo,
                ct.SoLuongHuy,
                ct.LyDoHuy,
                ct.GhiChu,
                // Thông tin từ TonKho với include Thuoc
                TenThuoc = ct.TonKho?.Thuoc?.TenThuoc,
                HanSuDung = ct.TonKho?.HanSuDung,
                DonViTinh = ct.TonKho?.MaLoaiDonViTinh
            }).ToList();

            return new { PhieuHuy = phieuHuy, ChiTiet = chiTiet };
        }
    }
}