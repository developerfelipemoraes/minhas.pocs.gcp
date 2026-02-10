using Microsoft.AspNetCore.Mvc;
using OracleIDE.Models;
using OracleIDE.Services;
using Oracle.ManagedDataAccess.Client;
using System.Diagnostics;

namespace OracleIDE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly OracleConnectionManager _connManager;

    public DatabaseController(OracleConnectionManager connManager)
    {
        _connManager = connManager;
    }

    [HttpPost("connect")]
    public async Task<ActionResult<ConnectionResult>> Connect([FromBody] ConnectionRequest req)
    {
        try
        {
            var version = await _connManager.ConnectAsync(req.Host, req.Port, req.ServiceName, req.User, req.Password);
            return Ok(new ConnectionResult
            {
                Success = true,
                Message = $"Conectado com sucesso a {req.Host}:{req.Port}/{req.ServiceName}",
                ServerVersion = version
            });
        }
        catch (Exception ex)
        {
            return Ok(new ConnectionResult { Success = false, Message = ex.Message });
        }
    }

    [HttpPost("test")]
    public async Task<ActionResult<ConnectionResult>> TestConnection([FromBody] ConnectionRequest req)
    {
        try
        {
            var version = await _connManager.TestConnectionAsync(req.Host, req.Port, req.ServiceName, req.User, req.Password);
            return Ok(new ConnectionResult
            {
                Success = true,
                Message = $"Conexão OK — {version}",
                ServerVersion = version
            });
        }
        catch (Exception ex)
        {
            return Ok(new ConnectionResult { Success = false, Message = $"Falha na conexão: {ex.Message}" });
        }
    }

    [HttpPost("disconnect")]
    public ActionResult<ConnectionResult> Disconnect()
    {
        _connManager.Disconnect();
        return Ok(new ConnectionResult { Success = true, Message = "Desconectado" });
    }

    [HttpGet("status")]
    public ActionResult Status()
    {
        return Ok(new { connected = _connManager.IsConnected });
    }

    [HttpPost("execute")]
    public async Task<ActionResult<QueryResult>> Execute([FromBody] QueryRequest req)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (!_connManager.IsConnected)
                return Ok(new QueryResult { Success = false, Message = "Não conectado ao banco de dados." });

            var sql = req.Sql?.Trim() ?? "";
            if (string.IsNullOrEmpty(sql))
                return Ok(new QueryResult { Success = false, Message = "SQL vazio." });

            var conn = _connManager.GetConnection();
            var queryType = DetectQueryType(sql);

            using var cmd = new OracleCommand(sql, conn);
            cmd.CommandTimeout = 60;

            if (queryType == "SELECT")
            {
                using var reader = await cmd.ExecuteReaderAsync();

                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                    columns.Add(reader.GetName(i));

                var rows = new List<List<object?>>();
                int maxRows = 5000; // safety limit
                int count = 0;

                while (await reader.ReadAsync() && count < maxRows)
                {
                    var row = new List<object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.IsDBNull(i))
                            row.Add(null);
                        else
                        {
                            var val = reader.GetValue(i);
                            // Convert Oracle types to string for JSON serialization
                            row.Add(val?.ToString());
                        }
                    }
                    rows.Add(row);
                    count++;
                }

                sw.Stop();
                var msg = count >= maxRows
                    ? $"Exibindo primeiras {maxRows} linhas (limite de segurança). Query executada em {sw.ElapsedMilliseconds}ms"
                    : $"{count} linha(s) retornada(s) em {sw.ElapsedMilliseconds}ms";

                return Ok(new QueryResult
                {
                    Success = true,
                    Message = msg,
                    QueryType = queryType,
                    Columns = columns,
                    Rows = rows,
                    RowsAffected = count,
                    ElapsedMs = sw.ElapsedMilliseconds
                });
            }
            else
            {
                var affected = await cmd.ExecuteNonQueryAsync();
                sw.Stop();

                // Auto-commit for DML
                if (queryType is "UPDATE" or "INSERT" or "DELETE" or "MERGE")
                {
                    using var commitCmd = new OracleCommand("COMMIT", conn);
                    await commitCmd.ExecuteNonQueryAsync();
                }

                return Ok(new QueryResult
                {
                    Success = true,
                    Message = queryType switch
                    {
                        "UPDATE" => $"{affected} linha(s) atualizada(s) em {sw.ElapsedMilliseconds}ms (COMMIT automático)",
                        "INSERT" => $"{affected} linha(s) inserida(s) em {sw.ElapsedMilliseconds}ms (COMMIT automático)",
                        "DELETE" => $"{affected} linha(s) removida(s) em {sw.ElapsedMilliseconds}ms (COMMIT automático)",
                        "MERGE" => $"{affected} linha(s) afetada(s) em {sw.ElapsedMilliseconds}ms (COMMIT automático)",
                        _ => $"Comando executado com sucesso em {sw.ElapsedMilliseconds}ms"
                    },
                    QueryType = queryType,
                    RowsAffected = affected,
                    ElapsedMs = sw.ElapsedMilliseconds
                });
            }
        }
        catch (OracleException ex)
        {
            sw.Stop();
            return Ok(new QueryResult
            {
                Success = false,
                Message = $"ORA-{ex.Number}: {ex.Message}",
                ElapsedMs = sw.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Ok(new QueryResult
            {
                Success = false,
                Message = $"Erro: {ex.Message}",
                ElapsedMs = sw.ElapsedMilliseconds
            });
        }
    }

    private static string DetectQueryType(string sql)
    {
        var trimmed = sql.TrimStart().ToUpperInvariant();
        if (trimmed.StartsWith("SELECT") || trimmed.StartsWith("WITH")) return "SELECT";
        if (trimmed.StartsWith("UPDATE")) return "UPDATE";
        if (trimmed.StartsWith("INSERT")) return "INSERT";
        if (trimmed.StartsWith("DELETE")) return "DELETE";
        if (trimmed.StartsWith("MERGE")) return "MERGE";
        if (trimmed.StartsWith("CREATE")) return "DDL";
        if (trimmed.StartsWith("ALTER")) return "DDL";
        if (trimmed.StartsWith("DROP")) return "DDL";
        if (trimmed.StartsWith("TRUNCATE")) return "DDL";
        if (trimmed.StartsWith("BEGIN") || trimmed.StartsWith("DECLARE")) return "PLSQL";
        if (trimmed.StartsWith("COMMIT")) return "TCL";
        if (trimmed.StartsWith("ROLLBACK")) return "TCL";
        return "OTHER";
    }
}
