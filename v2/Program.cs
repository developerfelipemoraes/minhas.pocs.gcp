using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http.Features;
using Aurovel.GcsUpload.Options;
using Aurovel.GcsUpload.Services;

var builder = WebApplication.CreateBuilder(args);

// Aceitar payloads grandes
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = null;
    o.Limits.MinRequestBodyDataRate = null;
});

builder.Services.Configure<FormOptions>(opts =>
{
    opts.MultipartBodyLengthLimit = 1L * 1024 * 1024 * 1024; // 1 GB
});

// Configuração GCP
builder.Services.Configure<GcsOptions>(builder.Configuration.GetSection("Gcp"));

// Serviços
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IGcsResumableSigner, GcsResumableSigner>();
builder.Services.AddSingleton<IResumableUploader, ResumableUploader>();
builder.Services.AddSingleton<IDirectUploader, DirectUploader>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();