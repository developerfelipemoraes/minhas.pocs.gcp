using Google.Cloud.Storage.V1;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<UrlSigner>(_ =>
    UrlSigner.FromCredentialFile("gcs-signer.json"));

// CORS para a SPA (ajuste origins conforme necessário)
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("spa", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .SetIsOriginAllowed(_ => true) // POC: libera tudo. Em prod, restrinja.
        .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GCS Signed URL PoC",
        Version = "v1",
        Description = "API: gera Signed URL V4 para upload resumable e provê download (signed e proxy)."
    });
});

// StorageClient via ADC (GKE Workload Identity, gcloud ADC, etc.)
builder.Services.AddSingleton(StorageClient.Create());

var app = builder.Build();

app.UseCors("spa");
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
