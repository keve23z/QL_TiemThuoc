-- Tạo bảng PhieuHuy
CREATE TABLE PhieuHuy (
    MaPH VARCHAR(20) PRIMARY KEY,
    NgayHuy DATETIME NOT NULL DEFAULT GETDATE(),
    MaNV VARCHAR(10) NOT NULL,
    LoaiHuy VARCHAR(10) NOT NULL CHECK (LoaiHuy IN ('KHO', 'HOADON')), -- KHO: hủy từ kho, HOADON: hủy từ hóa đơn
    MaHD VARCHAR(20) NULL, -- Mã hóa đơn (nếu hủy từ hóa đơn)
    TongSoLuongHuy DECIMAL(18,2) NOT NULL DEFAULT 0,
    GhiChu NVARCHAR(1000),

    CONSTRAINT FK_PhieuHuy_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MANV),
    CONSTRAINT FK_PhieuHuy_HoaDon FOREIGN KEY (MaHD) REFERENCES HoaDon(MaHD)
);

-- Tạo bảng ChiTietPhieuHuy
CREATE TABLE ChiTietPhieuHuy (
    MaPH VARCHAR(20) NOT NULL,
    MaLo VARCHAR(20) NOT NULL,
    SoLuongHuy DECIMAL(18,2) NOT NULL CHECK (SoLuongHuy > 0),
    LyDoHuy NVARCHAR(500) NOT NULL,
    GhiChu NVARCHAR(1000),

    PRIMARY KEY (MaPH, MaLo),
    CONSTRAINT FK_ChiTietPhieuHuy_PhieuHuy FOREIGN KEY (MaPH) REFERENCES PhieuHuy(MaPH),
    CONSTRAINT FK_ChiTietPhieuHuy_TonKho FOREIGN KEY (MaLo) REFERENCES TonKho(MaLo)
);

-- Indexes
CREATE INDEX IX_PhieuHuy_NgayHuy ON PhieuHuy(NgayHuy);
CREATE INDEX IX_PhieuHuy_MaNV ON PhieuHuy(MaNV);
CREATE INDEX IX_PhieuHuy_LoaiHuy ON PhieuHuy(LoaiHuy);
CREATE INDEX IX_PhieuHuy_MaHD ON PhieuHuy(MaHD);
CREATE INDEX IX_ChiTietPhieuHuy_MaLo ON ChiTietPhieuHuy(MaLo);