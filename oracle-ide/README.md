# Oracle Query IDE — ASP.NET Core

IDE web para executar queries Oracle **sem precisar do Oracle Client instalado**.
Usa o driver 100% gerenciado `Oracle.ManagedDataAccess.Core`.

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (ou .NET 6/7 — ajuste o `TargetFramework` no `.csproj`)
- Acesso de rede ao banco Oracle

**NÃO precisa de:**
- ❌ Oracle Client
- ❌ Oracle Instant Client
- ❌ ORACLE_HOME / TNS_ADMIN
- ❌ tnsnames.ora

## Como rodar

```bash
cd OracleIDE
dotnet restore
dotnet run
```

Abra o navegador em **http://localhost:5000**

## Estrutura do Projeto

```
OracleIDE/
├── Controllers/
│   └── DatabaseController.cs   # API REST (connect, test, execute, disconnect)
├── Models/
│   └── Models.cs               # Request/Response DTOs
├── Services/
│   └── OracleConnectionManager.cs  # Gerenciamento de conexão Oracle
├── Properties/
│   └── launchSettings.json
├── wwwroot/
│   └── index.html              # Frontend completo (single-file)
├── Program.cs                  # Entry point
├── appsettings.json
└── OracleIDE.csproj
```

## Funcionalidades

- ✅ Conectar/desconectar do Oracle via interface web
- ✅ Executar SELECT, INSERT, UPDATE, DELETE, DDL
- ✅ Grid de resultados com até 5000 linhas
- ✅ Auto-COMMIT para DML (UPDATE/INSERT/DELETE)
- ✅ Templates rápidos de SQL
- ✅ Histórico de queries
- ✅ Gerador de código .NET automático
- ✅ Atalhos: Ctrl+Enter / F5 para executar
- ✅ Tratamento de erros Oracle (ORA-XXXXX)
- ✅ Painel de mensagens

## API Endpoints

| Método | Endpoint              | Descrição              |
|--------|-----------------------|------------------------|
| POST   | /api/database/connect    | Conectar ao Oracle  |
| POST   | /api/database/test       | Testar conexão      |
| POST   | /api/database/disconnect | Desconectar         |
| GET    | /api/database/status     | Status da conexão   |
| POST   | /api/database/execute    | Executar SQL        |

## Observações

- A conexão é mantida como **Singleton** no servidor (ideal para uso single-user / desenvolvimento)
- Para uso multiusuário, implemente sessões ou connection pooling
- O limite padrão de linhas retornadas é 5000 (configurável no controller)
- DML executa COMMIT automático — remova se quiser controle manual de transação
