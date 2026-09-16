#!/usr/bin/env bash
# ==============================================================================
# Script tự động sao lưu PostgreSQL cho NamViet ERP trên môi trường Linux VPS
# Đặt vào cron chạy hằng đêm: 0 2 * * * /path/to/backup-db-linux.sh >> /var/log/namviet-backup.log 2>&1
# ==============================================================================

set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/var/backups/namviet-erp}"
KEEP_DAYS="${KEEP_DAYS:-30}"
DB_NAME="${POSTGRES_DB:-namviet_erp}"
DB_USER="${POSTGRES_USER:-erp_admin}"
DB_HOST="${POSTGRES_HOST:-127.0.0.1}"
DB_PORT="${POSTGRES_PORT:-5432}"

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
FILENAME="${BACKUP_DIR}/${DB_NAME}_${TIMESTAMP}.dump"

mkdir -p "${BACKUP_DIR}"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Bắt đầu sao lưu database ${DB_NAME} từ ${DB_HOST}:${DB_PORT}..."

# Nếu chạy trong Docker container postgres
if command -v docker &> /dev/null && docker ps --format '{{.Names}}' | grep -q "namviet-erp-postgres"; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Phát hiện container namviet-erp-postgres, thực hiện pg_dump qua docker exec..."
    docker exec -e PGPASSWORD="${POSTGRES_PASSWORD:-}" namviet-erp-postgres \
        pg_dump -U "${DB_USER}" -d "${DB_NAME}" --format=custom --no-owner --no-privileges > "${FILENAME}"
else
    # Chạy trực tiếp từ pg_dump của host
    export PGPASSWORD="${POSTGRES_PASSWORD:-}"
    pg_dump -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" \
        --format=custom --no-owner --no-privileges --file="${FILENAME}"
fi

FILE_SIZE=$(du -h "${FILENAME}" | cut -f1)
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Sao lưu thành công: ${FILENAME} (${FILE_SIZE})"

# Dọn dẹp backup cũ quá $KEEP_DAYS ngày
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Xóa các bản sao lưu cũ hơn ${KEEP_DAYS} ngày..."
find "${BACKUP_DIR}" -type f -name "${DB_NAME}_*.dump" -mtime +"${KEEP_DAYS}" -exec rm -f {} +

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Hoàn tất quy trình sao lưu an toàn."
