# Aurovel.GcsUpload (Controllers) — Duas opções de upload para GCS

1) **Resumable via Signed URL (V4)** — robusto para ~500 MB+  
   `POST /api/upload/resumable` (multipart/form-data: `file=@...`)

2) **Direto (byte array / stream)** — sem Signed URL, usando Service Account  
   `POST /api/upload/direct-bytes` (body: `application/octet-stream`, header opcional `X-File-Name`)

## Como rodar
- Ajuste `Gcp` no `appsettings.json` (bucket e credenciais).
- `dotnet restore && dotnet run`

## Exemplos

### Resumable (multipart)
```bash
curl -X POST "http://localhost:5073/api/upload/resumable"   -F "file=@/path/arquivo_500mb.bin;type=application/octet-stream"
```

### Direto (raw bytes no corpo)
```bash
curl -X POST "http://localhost:5073/api/upload/direct-bytes?fileName=arquivo.bin&contentType=application/octet-stream"   -H "Content-Type: application/octet-stream"   --data-binary "@/path/arquivo.bin"
```

Ou usando header para nome:
```bash
curl -X POST "http://localhost:5073/api/upload/direct-bytes"   -H "Content-Type: application/octet-stream"   -H "X-File-Name: arquivo.bin"   --data-binary "@/path/arquivo.bin"
```