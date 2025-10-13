# TestUploader — Resumable Signed URL (GCS)

Versão corrigida para `System.CommandLine` usando `InvocationContext` (evita o erro *No overload for method 'SetHandler' takes 11 arguments*).

## Build
```bash
dotnet build -c Release
```

## Uso rápido
```bash
dotnet run -c Release -- \
  --api-base "https://SEU-HOST/api/uploads" \
  --download-base "https://SEU-HOST/v1/files" \
  --bucket "filesmanager"
```

Outras flags: `--token`, `--concurrency`, `--files`, `--prefix`, `--debug`.
