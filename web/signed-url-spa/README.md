# SPA (Vite + React + TS) — Upload Resumable com Signed URL & Download

## Configuração
- `VITE_API_BASE_URL` (opcional): URL da API. Default `http://localhost:8080`.

## Rodando
```bash
cd web/signed-url-spa
npm i
npm run dev
# abre http://localhost:5173
```

### Fluxo
1. Selecione um arquivo (p.ex. 100 MB).
2. Clique em **Enviar**: a SPA chama `POST /api/uploads/signed-resumable` para obter uma **Signed URL**.
3. A SPA inicia a sessão (POST + header `x-goog-resumable: start`) e envia **em chunks** via `PUT` com `Content-Range`.
4. Após concluir, você pode:
   - Gerar **signed download URL** e abrir em nova aba.
   - Fazer **download via API** (`/api/files/proxy?objectName=...`).

> Observação: Para retomar uploads após falhas, implemente a sonda com `PUT` e `Content-Range: bytes */total` para obter o último byte persistido (não incluído nesta POC).
