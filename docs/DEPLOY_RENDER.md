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
| Branch | `feature/purchasing` (hoặc `production` nếu đã merge) |
| Runtime | **Docker** |
| Dockerfile Path | `./Dockerfile` |
| Instance type | **Free** (demo) |

5. **Trước khi Create**, mở **Environment** và thêm:

| Key | Value |
|-----|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | *(không cần set — Render tự inject; Dockerfile đã bind `${PORT}`)* |
| `ConnectionStrings__Default` | `Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.ufqgndvupcdmxqupxspl;Password=NvErp2026!Prod;SSL Mode=Require;Trust Server Certificate=true` |
| `Jwt__SigningKey` | `lR4!8xP2#qV9mK7tN3dF6wZ1cH5uJ0sB` |
| `Jwt__Issuer` | `namviet-erp` |
| `Jwt__Audience` | `namviet-erp-client` |
| `Jwt__AccessTokenMinutes` | `60` |
| `Cors__AllowedOrigins` | `https://namviet-erp-fe.vercel.app` |
| `AuthCookies__Secure` | `true` |
| `AuthCookies__SameSite` | `None` |

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
