-- ==============================================================================
-- Cấu hình tối ưu hóa PostgreSQL 16 cho hệ thống NamViet ERP (50 người dùng)
-- Phù hợp cho VPS cấu hình 2 vCPU, 2GB - 4GB RAM, ổ SSD.
-- Chạy trên database bằng psql hoặc pgAdmin (hoặc cấu hình trong postgresql.conf).
-- ==============================================================================

-- 1. Kết nối & Bộ nhớ phiên làm việc
-- 50 người dùng ERP thực tế chỉ dùng khoảng 15-30 kết nối đồng thời.
-- Đặt max_connections = 150 để vừa đủ cho 3 connection pool (Identity, Warehouse, HR) và backup.
ALTER SYSTEM SET max_connections = '150';

-- Bộ nhớ đệm dùng chung (Shared Buffers) ~ 25% tổng RAM (với VPS 2GB RAM là 512MB, VPS 4GB RAM là 1GB)
ALTER SYSTEM SET shared_buffers = '512MB';

-- Bộ nhớ đệm hiệu dụng cho bộ lập kế hoạch truy vấn (Effective Cache Size) ~ 50-75% RAM
ALTER SYSTEM SET effective_cache_size = '1536MB';

-- Bộ nhớ cho mỗi thao tác sắp xếp (ORDER BY, DISTINCT, HASH JOIN)
-- Đặt 16MB để các báo cáo kho, bảng kê chứng từ không phải ghi tạm ra ổ đĩa (Disk Spill)
ALTER SYSTEM SET work_mem = '16MB';

-- Bộ nhớ cho các tác vụ bảo trì (VACUUM, CREATE INDEX, ALTER TABLE)
ALTER SYSTEM SET maintenance_work_mem = '128MB';

-- 2. Tối ưu ghi đĩa & Checkpoint (Write-Ahead Logging - WAL)
ALTER SYSTEM SET wal_buffers = '16MB';
ALTER SYSTEM SET checkpoint_completion_target = '0.9';
ALTER SYSTEM SET min_wal_size = '1GB';
ALTER SYSTEM SET max_wal_size = '4GB';

-- 3. Chi phí truy vấn cho ổ cứng thể rắn (SSD / NVMe)
-- Ổ SSD có tốc độ đọc ngẫu nhiên rất nhanh, giúp Postgres ưu tiên dùng Index Scan hơn Seq Scan
ALTER SYSTEM SET random_page_cost = '1.1';
ALTER SYSTEM SET effective_io_concurrency = '200';

-- 4. Tự động dọn dẹp (Autovacuum) chạy thường xuyên để giữ bảng sạch, tránh bloat
ALTER SYSTEM SET autovacuum = 'on';
ALTER SYSTEM SET autovacuum_vacuum_scale_factor = '0.05';
ALTER SYSTEM SET autovacuum_analyze_scale_factor = '0.02';

-- Áp dụng:
-- Sau khi chạy lệnh này, restart service PostgreSQL để các thông số có hiệu lực:
-- sudo systemctl restart postgresql (hoặc restart docker container postgres)
