# Deploy Backend API lên Render — namviet-erp-api

Repo BE cần có `Dockerfile` ở root (đã thêm). FE đã ở: https://namviet-erp-fe.vercel.app

## 1) Đăng ký / đăng nhập Render

1. Mở https://render.com → Sign up (GitHub nhanh nhất)
2. Authorize GitHub org **NamViet-ERP** nếu được hỏi

## 2) Tạo Web Service

1. Dashboard → **New +** → **Web Service**
2. Connect repo **`NamViet-ERP/namviet-erp-be`**
3. Nếu repo private: **Configure account** → grant access cho org
4. Điền:

| Field | Value |
|-------|--------|
| Name | `namviet-erp-api` |
| Region | Singapore (gần Supabase) |
| Branch | `main` |
| Runtime | **Docker** |
| Dockerfile Path | `./Dockerfile` |
| Instance type | **Free** (demo) |

5. **Trước khi Create**, mở **Environment** và thêm:

> **CẢNH BÁO BẢO MẬT:** Phiên bản cũ của file này từng chứa connection string và JWT signing key **thật**. Các giá trị đó coi như đã lộ - **bắt buộc rotate ngay**: đổi database password trong Supabase Dashboard (Settings → Database) và tạo `Jwt__SigningKey` mới trên Render. Không bao giờ commit giá trị thật vào repo.

| Key | Value |
|-----|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | *(không cần set — Render tự inject; Dockerfile đã bind `${PORT}`)* |
| `ConnectionStrings__Default` | `Host=<supabase-pooler-host>;Port=5432;Database=postgres;Username=<supabase-username>;Password=<DB_PASSWORD_MỚI>;SSL Mode=Require;Trust Server Certificate=true` *(lấy từ Supabase Dashboard, chỉ dán trực tiếp vào Render)* |
| `Jwt__SigningKey` | *(chuỗi ngẫu nhiên ≥ 48 ký tự, tạo mới bằng `openssl rand -base64 48`, chỉ dán vào Render)* |
| `Jwt__Issuer` | `namviet-erp` |
| `Jwt__Audience` | `namviet-erp-client` |
| `Jwt__AccessTokenMinutes` | `15` |
| `Cors__AllowedOrigins` | `https://namviet-erp-fe.vercel.app` |
| `AuthCookies__Secure` | `true` |
| `AuthCookies__SameSite` | `None` |
| `DOTNET_HOSTBUILDER_RELOADCONFIGONCHANGE` | `false` |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `1` |
| `FileStorage__Provider` | `S3` |
| `FileStorage__S3__ServiceUrl` | `https://<supabase-project-ref>.storage.supabase.co/storage/v1/s3` |
| `FileStorage__S3__BucketName` | `erp-uploads` |
| `FileStorage__S3__Region` | `ap-southeast-1` |
| `FileStorage__S3__AccessKey` | *(lấy từ Supabase: Project Settings → Storage → S3 Credentials)* |
| `FileStorage__S3__SecretKey` | *(lấy từ Supabase: Project Settings → Storage → S3 Credentials)* |
| `FileStorage__S3__ForcePathStyle` | `true` |

> **Lưu ý cấu hình Supabase Storage:**
> 1. Mở Supabase Dashboard → **Storage** → Bấm **New bucket** → đặt tên `erp-uploads` (chọn Private).
> 2. Vào **Project Settings** → **Storage** → phần **S3 Credentials** → Bấm **Generate new credentials** để lấy `Access Key ID` và `Secret Access Key`.
> 3. `<supabase-project-ref>` là mã dự án của bạn trên URL dashboard Supabase (`https://supabase.com/dashboard/project/<project-ref>`).
> Khi cấu hình S3, toàn bộ file đính kèm hợp đồng, PO, tuyển dụng sẽ được lưu bền vững trên Supabase Storage, không bao giờ bị mất khi Render restart/redeploy.

> Cookie SameSite=`None` + Secure=`true` cần khi FE (Vercel) và API (Render) **khác domain**. Nếu login cookie bị browser chặn, báo mình để chỉnh FE gọi absolute API URL.

6. Bấm **Create Web Service** → đợi build (~5–10 phút trên Free)

## 3) Lấy URL API

Sau khi status = **Live**, copy URL dạng:

`https://namviet-erp-api.onrender.com`

Kiểm tra:

```bash
curl https://namviet-erp-api.onrender.com/health
# Kỳ vọng: 200
```

## 4) Báo URL lại cho agent

Gửi URL API → mình sẽ rebuild FE Vercel với:

`VITE_API_BASE_URL=https://namviet-erp-api.onrender.com`

(và xác nhận CORS đã khớp).

## Lưu ý Free tier Render

- Service **sleep** sau ~15 phút không traffic → request đầu tiên có thể chậm 30–60s.
- Để demo mentor: mở sẵn URL API 1 lần trước khi demo login.
