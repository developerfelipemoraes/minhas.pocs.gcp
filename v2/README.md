# Aurovel.GcsUpload (Controllers) — 3 rotas de upload para GCS

1) **Resumable via Signed URL (V4)** — robusto p/ ~500 MB+  
   `POST /api/upload/resumable` (multipart/form-data: `file=@...`)

2) **Direto (raw bytes)** — sem Signed URL, usando Service Account  
   `POST /api/upload/direct-bytes` (body: `application/octet-stream`, header opcional `X-File-Name`)

3) **Direto (IFormFile)** — sem Signed URL, usando Service Account  
   `POST /api/upload/direct-form` (multipart/form-data: `file=@...`)

## Como rodar
- Ajuste `Gcp` no `appsettings.json` (bucket e credenciais).
- `dotnet restore && dotnet run`

## Exemplos
```bash
curl -X POST "http://localhost:5073/api/upload/resumable"   -F "file=@/path/arquivo_500mb.bin;type=application/octet-stream"

curl -X POST "http://localhost:5073/api/upload/direct-bytes?fileName=arquivo.bin&contentType=application/octet-stream"   -H "Content-Type: application/octet-stream"   --data-binary "@/path/arquivo.bin"

curl -X POST "http://localhost:5073/api/upload/direct-form"   -F "file=@/path/arquivo.bin;type=application/octet-stream"
```