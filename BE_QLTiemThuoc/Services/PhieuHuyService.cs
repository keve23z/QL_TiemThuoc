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

            // Kiểm tra chi tiết hóa đơn và hạn sử dụng
            var chiTietHoaDon = await _repo.GetChiTietHoaDonByMaHDAsync(dto.MaHD);
            if (chiTietHoaDon == null || !chiTietHoaDon.Any())
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHD} không có chi tiết");
            }

            // Lấy thông tin tồn kho cho các lô thuốc trong hóa đơn
            var maLoList = chiTietHoaDon.Where(ct => !string.IsNullOrEmpty(ct.MaLo)).Select(ct => ct.MaLo!).Distinct().ToList();
            var tonKhoDict = new Dictionary<string, Model.Kho.TonKho>();
            if (maLoList.Any())
            {
                var tonKhos = await _repo.GetTonKhoByMaLoListAsync(maLoList);
                foreach (var tk in tonKhos)
                {
                    tonKhoDict[tk.MaLo] = tk;
                }
            }

            // Validate các lô thuốc trong request có thuộc về hóa đơn này không
            foreach (var xuLy in dto.XuLyThuocs)
            {
                var chiTiet = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == xuLy.MaLo);
                if (chiTiet == null)
                {
                    throw new ArgumentException($"Lô thuốc {xuLy.MaLo} không thuộc về hóa đơn {dto.MaHD}");
                }

                // Kiểm tra hạn sử dụng nếu cần hủy
                if (xuLy.LoaiXuLy == "HUY" && tonKhoDict.TryGetValue(xuLy.MaLo, out var tonKho) && tonKho.HanSuDung != null)
                {
                                        var ngayHetHan = tonKho.HanSuDung;
                                        var thangConLai = (tonKho.HanSuDung - DateTime.Now).TotalDays / 30;

                    // Nếu thuốc còn hạn > 6 tháng, cảnh báo nhưng vẫn cho phép hủy nếu có lý do đặc biệt
                    if (thangConLai > 6 && string.IsNullOrEmpty(xuLy.LyDoHuy))
                    {
                        throw new ArgumentException($"Thuốc {xuLy.MaLo} còn hạn {thangConLai:F1} tháng. Cần có lý do hủy cụ thể.");
                    }
                }
            }

            var result = new
            {
                PhieuHuy = (object?)null,
                ThuocHoanLai = new List<object>(),
                TongSoLuongHuy = 0,
                TongSoLuongHoanLai = 0,
                Message = ""
            };

            // Tách thuốc cần hủy và thuốc có thể hoàn lại
            var thuocCanHuy = dto.XuLyThuocs.Where(t => t.LoaiXuLy == "HUY").ToList();
            var thuocHoanLai = dto.XuLyThuocs.Where(t => t.LoaiXuLy == "HOAN_LAI").ToList();

            // Xử lý thuốc hoàn lại vào kho
            var thuocHoanLaiDetails = new List<object>();
            foreach (var thuoc in thuocHoanLai)
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == thuoc.MaLo);
                if (chiTietHD != null && tonKhoDict.TryGetValue(thuoc.MaLo, out var tonKho))
                {
                    // Hoàn lại số lượng từ chi tiết hóa đơn vào kho
                    tonKho.SoLuongCon += chiTietHD.SoLuong;

                    await _repo.UpdateTonKhoAsync(tonKho);

                    thuocHoanLaiDetails.Add(new
                    {
                        thuoc.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        SoLuongHoanLai = chiTietHD.SoLuong,
                        thuoc.GhiChu
                    });
                }
            }

            // Xử lý thuốc cần hủy
            object? phieuHuyResult = null;
            if (thuocCanHuy.Any())
            {
                var phieuHuyDto = new PhieuHuyDto
                {
                    MaNV = dto.MaNV,
                    LoaiHuy = "HOADON",
                    MaHD = dto.MaHD,
                    GhiChu = dto.GhiChu,
                    ChiTietPhieuHuys = thuocCanHuy.Select(t =>
                    {
                        var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                        return new ChiTietPhieuHuyDto
                        {
                            MaLo = t.MaLo,
                            SoLuongHuy = t.SoLuongHuy ?? (chiTietHD?.SoLuong ?? 0),
                            LyDoHuy = t.LyDoHuy ?? "Hủy từ hóa đơn khách hàng",
                            GhiChu = t.GhiChu
                        };
                    }).ToList()
                };

                phieuHuyResult = await CreatePhieuHuyAsync(phieuHuyDto);
            }

            // Cập nhật trạng thái hóa đơn sau khi xử lý
            await UpdateHoaDonStatusAfterHuyAsync(dto.MaHD);

            var tongSoLuongHuy = thuocCanHuy.Sum(t => t.SoLuongHuy ?? 0);
            var tongSoLuongHoanLai = thuocHoanLai.Sum(t =>
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                return chiTietHD?.SoLuong ?? 0;
            });

            return new
            {
                PhieuHuy = phieuHuyResult,
                ThuocHoanLai = thuocHoanLaiDetails,
                TongSoLuongHuy = tongSoLuongHuy,
                TongSoLuongHoanLai = tongSoLuongHoanLai,
                Message = $"Đã xử lý hủy hóa đơn: hủy {tongSoLuongHuy} sản phẩm, hoàn lại {tongSoLuongHoanLai} sản phẩm vào kho"
            };
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

        public async Task<object> HuyTuKhoAsync(HuyTuKhoDto dto)
        {
            // Validation
            if (string.IsNullOrEmpty(dto.MaNhanVien))
            {
                throw new ArgumentException("MaNhanVien is required");
            }

            if (dto.ChiTietPhieuHuy == null || !dto.ChiTietPhieuHuy.Any())
            {
                throw new ArgumentException("ChiTietPhieuHuy cannot be empty");
            }

            // Validate tồn kho cho từng lô
            foreach (var chiTiet in dto.ChiTietPhieuHuy)
            {
                var tonKho = await _repo.GetTonKhoByMaLoAsync(chiTiet.MaLo);
                if (tonKho == null)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} không tồn tại trong kho");
                }

                if (tonKho.SoLuongCon < chiTiet.SoLuongHuy)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} chỉ còn {tonKho.SoLuongCon} không đủ để hủy {chiTiet.SoLuongHuy}");
                }

                // Kiểm tra hạn sử dụng - ưu tiên hủy thuốc sắp hết hạn
                var thangConLai = (tonKho.HanSuDung - DateTime.Now).TotalDays / 30;
                if (thangConLai > 12 && string.IsNullOrEmpty(chiTiet.LyDoChiTiet))
                {
                    throw new ArgumentException($"Thuốc {chiTiet.MaLo} còn hạn {thangConLai:F1} tháng. Cần có lý do hủy cụ thể.");
                }
            }

            // Tạo phiếu hủy từ kho
            var phieuHuyDto = new PhieuHuyDto
            {
                MaNV = dto.MaNhanVien,
                LoaiHuy = "KHO",
                MaHD = null,
                GhiChu = dto.LyDoHuy,
                ChiTietPhieuHuys = dto.ChiTietPhieuHuy.Select(c => new ChiTietPhieuHuyDto
                {
                    MaLo = c.MaLo,
                    SoLuongHuy = c.SoLuongHuy,
                    LyDoHuy = c.LyDoChiTiet,
                    GhiChu = c.LyDoChiTiet
                }).ToList()
            };

            var result = await CreatePhieuHuyAsync(phieuHuyDto);

            return new
            {
                MaPhieuHuy = ((dynamic)result).PhieuHuy?.MaPH,
                NgayHuy = ((dynamic)result).PhieuHuy?.NgayHuy,
                TongSoLuongHuy = dto.ChiTietPhieuHuy.Sum(c => c.SoLuongHuy),
                ChiTietPhieuHuy = dto.ChiTietPhieuHuy.Select(c =>
                {
                    var tonKho = _repo.GetTonKhoByMaLoAsync(c.MaLo).Result;
                    return new
                    {
                        c.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        c.SoLuongHuy,
                        DonGia = 0 // TonKho không có DonGia, có thể lấy từ ChiTietPhieuNhap sau
                    };
                }).ToList(),
                Message = "Tạo phiếu hủy từ kho thành công"
            };
        }

        public async Task<object> HuyTuHoaDonAsync(HuyTuHoaDonDto dto)
        {
            // Validation
            if (string.IsNullOrEmpty(dto.MaHoaDon))
            {
                throw new ArgumentException("MaHoaDon is required");
            }

            if (string.IsNullOrEmpty(dto.MaNhanVien))
            {
                throw new ArgumentException("MaNhanVien is required");
            }

            if (dto.ChiTietXuLy == null || !dto.ChiTietXuLy.Any())
            {
                throw new ArgumentException("ChiTietXuLy cannot be empty");
            }

            // Kiểm tra hóa đơn tồn tại
            var hoaDon = await _repo.GetHoaDonByIdAsync(dto.MaHoaDon);
            if (hoaDon == null)
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHoaDon} không tồn tại");
            }

            // Kiểm tra chi tiết hóa đơn
            var chiTietHoaDon = await _repo.GetChiTietHoaDonByMaHDAsync(dto.MaHoaDon);
            if (chiTietHoaDon == null || !chiTietHoaDon.Any())
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHoaDon} không có chi tiết");
            }

            // Lấy thông tin tồn kho cho các lô thuốc trong hóa đơn
            var maLoList = chiTietHoaDon.Where(ct => !string.IsNullOrEmpty(ct.MaLo)).Select(ct => ct.MaLo!).Distinct().ToList();
            var tonKhoDict = new Dictionary<string, Model.Kho.TonKho>();
            if (maLoList.Any())
            {
                var tonKhos = await _repo.GetTonKhoByMaLoListAsync(maLoList);
                foreach (var tk in tonKhos)
                {
                    tonKhoDict[tk.MaLo] = tk;
                }
            }

            // Validate các lô thuốc trong request
            foreach (var xuLy in dto.ChiTietXuLy)
            {
                var chiTiet = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == xuLy.MaLo);
                if (chiTiet == null)
                {
                    throw new ArgumentException($"Lô thuốc {xuLy.MaLo} không thuộc về hóa đơn {dto.MaHoaDon}");
                }

                // Kiểm tra hạn sử dụng
                if (xuLy.LoaiXuLy == "HUY" && tonKhoDict.TryGetValue(xuLy.MaLo, out var tonKho))
                {
                    var thangConLai = (tonKho.HanSuDung - DateTime.Now).TotalDays / 30;
                    if (thangConLai > 6 && string.IsNullOrEmpty(xuLy.LyDoChiTiet))
                    {
                        throw new ArgumentException($"Thuốc {xuLy.MaLo} còn hạn {thangConLai:F1} tháng. Cần có lý do hủy cụ thể.");
                    }
                }
            }

            var thuocCanHuy = dto.ChiTietXuLy.Where(t => t.LoaiXuLy == "HUY").ToList();
            var thuocHoanLai = dto.ChiTietXuLy.Where(t => t.LoaiXuLy == "HOAN_LAI").ToList();

            // Xử lý hoàn lại kho
            var thuocHoanLaiDetails = new List<object>();
            foreach (var thuoc in thuocHoanLai)
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == thuoc.MaLo);
                if (chiTietHD != null && tonKhoDict.TryGetValue(thuoc.MaLo, out var tonKho))
                {
                    tonKho.SoLuongCon += chiTietHD.SoLuong;
                    await _repo.UpdateTonKhoAsync(tonKho);

                    thuocHoanLaiDetails.Add(new
                    {
                        thuoc.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        SoLuongHoanLai = chiTietHD.SoLuong,
                        thuoc.LyDoChiTiet
                    });
                }
            }

            // Xử lý hủy
            object? phieuHuyResult = null;
            if (thuocCanHuy.Any())
            {
                var phieuHuyDto = new PhieuHuyDto
                {
                    MaNV = dto.MaNhanVien,
                    LoaiHuy = "HOADON",
                    MaHD = dto.MaHoaDon,
                    GhiChu = dto.LyDoHuy,
                    ChiTietPhieuHuys = thuocCanHuy.Select(t =>
                    {
                        var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                        return new ChiTietPhieuHuyDto
                        {
                            MaLo = t.MaLo,
                            SoLuongHuy = t.SoLuongHuy ?? (chiTietHD?.SoLuong ?? 0),
                            LyDoHuy = t.LyDoChiTiet ?? "Hủy từ hóa đơn khách hàng",
                            GhiChu = t.LyDoChiTiet
                        };
                    }).ToList()
                };

                phieuHuyResult = await CreatePhieuHuyAsync(phieuHuyDto);
            }

            // Cập nhật trạng thái hóa đơn
            await UpdateHoaDonStatusAfterHuyAsync(dto.MaHoaDon);

            var tongSoLuongHuy = thuocCanHuy.Sum(t => t.SoLuongHuy ?? 0);
            var tongSoLuongHoanLai = thuocHoanLai.Sum(t =>
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                return chiTietHD?.SoLuong ?? 0;
            });

            return new
            {
                MaPhieuHuy = ((dynamic)phieuHuyResult)?.PhieuHuy?.MaPH,
                MaHoaDon = dto.MaHoaDon,
                TrangThaiGiaoHang = hoaDon.TrangThaiGiaoHang,
                TongSoLuongHuy = tongSoLuongHuy,
                TongSoLuongHoanLai = tongSoLuongHoanLai,
                ChiTietXuLy = dto.ChiTietXuLy.Select(t =>
                {
                    var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                    tonKhoDict.TryGetValue(t.MaLo, out var tonKho);
                    return new
                    {
                        t.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        t.LoaiXuLy,
                        SoLuong = t.LoaiXuLy == "HUY" ? t.SoLuongHuy : chiTietHD?.SoLuong
                    };
                }).ToList(),
                Message = $"Xử lý hủy hóa đơn thành công: hủy {tongSoLuongHuy} sản phẩm, hoàn lại {tongSoLuongHoanLai} sản phẩm"
            };
        }

        public async Task<object> CreatePhieuHuyAsync(PhieuHuyDto phieuHuyDto)
        {
            // Validation
            if (phieuHuyDto == null || phieuHuyDto.ChiTietPhieuHuys == null || !phieuHuyDto.ChiTietPhieuHuys.Any())
            {
                throw new ArgumentException("Invalid input data or empty ChiTietPhieuHuys");
            }

            // Tạo phiếu hủy
            var phieuHuy = new PhieuHuy
            {
                MaPH = GenerateMaPhieuHuy(),
                NgayHuy = DateTime.Now,
                MaNV = phieuHuyDto.MaNV,
                LoaiHuy = phieuHuyDto.LoaiHuy,
                MaHD = phieuHuyDto.MaHD,
                TongSoLuongHuy = (decimal)phieuHuyDto.ChiTietPhieuHuys.Sum(c => c.SoLuongHuy)
            };

            // Tạo chi tiết phiếu hủy
            var chiTietPhieuHuyList = new List<ChiTietPhieuHuy>();
            foreach (var chiTiet in phieuHuyDto.ChiTietPhieuHuys)
            {
                var tonKho = await _repo.GetTonKhoByMaLoAsync(chiTiet.MaLo);
                if (tonKho == null)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} không tồn tại trong kho");
                }

                if (tonKho.SoLuongCon < chiTiet.SoLuongHuy)
                {
                    throw new ArgumentException($"Không đủ số lượng lô {chiTiet.MaLo} trong kho");
                }

                // Giảm số lượng trong kho
                tonKho.SoLuongCon -= (int)chiTiet.SoLuongHuy;
                await _repo.UpdateTonKhoAsync(tonKho);

                chiTietPhieuHuyList.Add(new ChiTietPhieuHuy
                {
                    MaPH = phieuHuy.MaPH,
                    MaLo = chiTiet.MaLo,
                    SoLuongHuy = (decimal)chiTiet.SoLuongHuy,
                    LyDoHuy = chiTiet.LyDoHuy,
                    GhiChu = chiTiet.GhiChu
                });
            }

            // Lưu phiếu hủy và chi tiết
            await _repo.AddAsync(phieuHuy);
            await _repo.AddChiTietRangeAsync(chiTietPhieuHuyList);

            return new
            {
                MaPhieuHuy = phieuHuy.MaPH,
                NgayHuy = phieuHuy.NgayHuy,
                TongSoLuongHuy = phieuHuy.TongSoLuongHuy,
                Message = "Tạo phiếu hủy thành công"
            };
        }

        private string GenerateMaPhieuHuy()
        {
            return "PH" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private string GenerateMaChiTietPhieuHuy()
        {
            return "CTPH" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }
    }
}