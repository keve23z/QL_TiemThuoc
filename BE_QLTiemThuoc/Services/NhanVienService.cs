using System.Collections.Generic;
using System.Threading.Tasks;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Repositories;

namespace BE_QLTiemThuoc.Services
{
    public class NhanVienService
    {
        private readonly NhanVienRepository _repo;

        public NhanVienService(NhanVienRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<NhanVien>> GetAllAsync()
        {
            return await _repo.GetAllAsync();
        }

        public async Task<NhanVien?> GetByIdAsync(string id)
        {
            return await _repo.GetByIdAsync(id);
        }
    }
}