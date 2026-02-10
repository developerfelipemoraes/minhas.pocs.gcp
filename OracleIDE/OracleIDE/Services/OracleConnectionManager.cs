using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace OracleIDE.Services;

public class OracleConnectionManager : IDisposable
{
    private OracleConnection? _connection;
    private string _connectionString = string.Empty;

    public bool IsConnected => _connection?.State == ConnectionState.Open;
    public string CurrentConnectionString => _connectionString;

    public async Task<string> ConnectAsync(string host, string port, string serviceName, string user, string password)
    {
        try
        {
            Disconnect();

            _connectionString =  $"User Id={user};Password={password};Data Source=//{host}:{port}/{serviceName};Connection Timeout=15;";

            _connection = new OracleConnection(_connectionString);
            
            await _connection.OpenAsync();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT banner FROM v$version WHERE ROWNUM = 1";
            var version = await cmd.ExecuteScalarAsync();
            return version?.ToString() ?? "Oracle Database";
        }
        catch (Exception ex)
        {
            _connection?.Dispose();
            _connection = null;
            _connectionString = string.Empty;
            throw new Exception($"Erro ao conectar: {ex.Message}");
        }
    }

    public async Task<string> TestConnectionAsync(string host, string port, string serviceName, string user, string password)
    {
        var connStr = $"User Id={user};Password={password};Data Source={host}:{port}/{serviceName};Connection Timeout=10;";
        using var conn = new OracleConnection(connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT banner FROM v$version WHERE ROWNUM = 1";
        var version = await cmd.ExecuteScalarAsync();
        return version?.ToString() ?? "Oracle Database";
    }

    public void Disconnect()
    {
        if (_connection != null)
        {
            if (_connection.State == ConnectionState.Open)
                _connection.Close();
            _connection.Dispose();
            _connection = null;
            _connectionString = string.Empty;
        }
    }

    public OracleConnection GetConnection()
    {
        if (_connection == null || _connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Não conectado ao banco de dados.");
        return _connection;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
