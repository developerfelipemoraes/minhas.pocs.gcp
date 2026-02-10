using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Aurovel.GcsUpload.Options;

namespace Aurovel.GcsUpload.Services;

public sealed class GcsResumableSigner : IGcsResumableSigner
{
    private readonly UrlSigner _signer;
    private readonly string _bucket;

    public GcsResumableSigner(IOptions<GcsOptions> options)
    {
        var cfg = options.Value;
        _bucket = cfg.Bucket ?? throw new ArgumentNullException(nameof(cfg.Bucket));

        if (!string.IsNullOrWhiteSpace(cfg.ServiceAccountJson))
        {
            _signer = UrlSigner.FromServiceAccountData(System.Text.Encoding.UTF8.GetBytes(cfg.ServiceAccountJson));
        }
        else if (!string.IsNullOrWhiteSpace(cfg.ServiceAccountKeyPath))
        {
            _signer = UrlSigner.FromServiceAccountPath(cfg.ServiceAccountKeyPath!);
        }
        else
        {
            throw new InvalidOperationException(
                "Credenciais GCP não configuradas. Defina Gcp:ServiceAccountJson ou Gcp:ServiceAccountKeyPath.");
        }
    }

    public string CreateResumableStartUrl(string objectName, TimeSpan lifetime)
    {
        var expires = DateTimeOffset.UtcNow.Add(lifetime);

        // Para iniciar sessão resumível via XML API:
        // - Método: POST
        // - Header obrigatório: x-goog-resumable: start (precisa constar na assinatura)
        var headers = new[]
        {
            new KeyValuePair<string, IEnumerable<string>>("x-goog-resumable", new[] { "start" }),
        };

        return _signer.Sign(_bucket, objectName, expires, HttpMethod.Post, headers);
    }
}