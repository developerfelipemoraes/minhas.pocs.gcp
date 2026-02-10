namespace OracleIDE.Models;

public class ConnectionRequest
{
    public string Host { get; set; } = "localhost";
    public string Port { get; set; } = "1521";
    public string ServiceName { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}

public class QueryRequest
{
    public string Sql { get; set; } = "";
}

public class QueryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string QueryType { get; set; } = "";
    public List<string> Columns { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
    public int RowsAffected { get; set; }
    public long ElapsedMs { get; set; }
}

public class ConnectionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ServerVersion { get; set; }
}
