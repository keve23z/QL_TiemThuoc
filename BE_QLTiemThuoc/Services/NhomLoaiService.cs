using System.Collections.Generic;
using System.Threading.Tasks;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Repositories;
using BE_QLTiemThuoc.Dto;
using System.Linq;

namespace BE_QLTiemThuoc.Services
{
    

    public class NhomLoaiService
    {
        private readonly NhomLoaiRepository _repo;

        public NhomLoaiService(NhomLoaiRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<NhomLoai>> GetAllAsync()
        {
            return await _repo.GetAllAsync();
        }

        public async Task<NhomLoai> GetByIdAsync(string maNhom)
        {
            var item = await _repo.GetByIdAsync(maNhom);
            if (item == null) throw new System.Exception("Không tìm thấy nhóm loại.");
            return item;
        }

        public async Task<List<LoaiThuoc>> GetLoaiByNhomAsync(string maNhom)
        {
            return await _repo.GetLoaiByNhomAsync(maNhom);
        }

        public async Task<List<GroupWithLoaiDto>> GetGroupsWithLoaiAsync()
        {
            var groups = await _repo.GetAllAsync();
            var result = new List<GroupWithLoaiDto>();

            foreach (var g in groups)
            {
                var loai = await _repo.GetLoaiByNhomAsync(g.MaNhomLoai ?? string.Empty);
                var dto = new GroupWithLoaiDto
                {
                    MaNhomLoai = g.MaNhomLoai,
                    TenNhomLoai = g.TenNhomLoai,
                    Loai = loai.Select(l => new LoaiItemDto { MaLoaiThuoc = l.MaLoaiThuoc, TenLoaiThuoc = l.TenLoaiThuoc, Icon = l.Icon }).ToList()
                };
                result.Add(dto);
            }

            return result;
        }
    }
}
