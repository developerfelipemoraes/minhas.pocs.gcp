namespace Aurovel.GcsUpload.Services;

public interface IGcsResumableSigner
{
    /// Cria URL V4 assinada para iniciar sessão resumível (POST + x-goog-resumable: start).
    string CreateResumableStartUrl(string objectName, TimeSpan lifetime);
}