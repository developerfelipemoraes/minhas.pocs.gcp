# API (.NET 8) — Signed URL + Download

Endpoints:
- `POST /api/uploads/signed-resumable` → Signed URL V4 para iniciar **resumable upload**.
- `GET  /api/files/list?prefix=uploads/&pageSize=50` → lista objetos.
- `GET  /api/files/signed-download?objectName=...` → retorna **Signed URL** de download.
- `GET  /api/files/proxy?objectName=...` → faz **download via API** (stream).

Env:
- `GCS_BUCKET` obrigatório.
- Credenciais via **ADC** (Workload Identity em GKE ou `gcloud auth application-default login` local).

CORS da POC está liberado para qualquer origin (`SetIsOriginAllowed(_ => true)`). Em produção, restrinja.
