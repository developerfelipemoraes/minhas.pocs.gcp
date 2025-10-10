# GCS Signed URL — PoC: API (.NET 8) + SPA (Vite/React)

- **API** em ASP.NET Core com endpoints para:
  - gerar **Signed URL V4** para upload **resumable**;
  - listar, gerar **Signed Download URL**, e **proxy de download**.
- **SPA** em Vite/React/TS que:
  - recebe a **Signed URL**, envia o arquivo em **chunks** (Content-Range) e mostra progresso;
  - lista objetos e permite **download** via signed URL ou via API.

## Como rodar

### API
```bash
cd src/SignedUrlGcs.Api
export GCS_BUCKET="seu-bucket"
dotnet run
# Swagger: http://localhost:8080/swagger
```

### SPA
```bash
cd web/signed-url-spa
npm i
npm run dev
# http://localhost:5173
# Se a API estiver em outra URL:
# VITE_API_BASE_URL=http://localhost:8080 npm run dev
```

## Produção (GKE)
- Use **Workload Identity** (sem chave estática) para a API.
- Restrinja **CORS** à sua origem real da SPA.
- Ajuste TTL das signed URLs e políticas do bucket conforme segurança.

---

> Para arquivos de 50–100 MB, chunks de **16–32 MiB** costumam performar bem. Mantenha o cluster e o bucket na **mesma região**.
