var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<OracleIDE.Services.OracleConnectionManager>();

var app = builder.Build();

app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
