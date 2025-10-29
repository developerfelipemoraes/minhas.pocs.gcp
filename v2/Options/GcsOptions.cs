namespace Aurovel.GcsUpload.Options;

public sealed class GcsOptions
{
    public string Bucket { get; set; } = default!;
    public string? ServiceAccountKeyPath { get; set; }
    public string? ServiceAccountJson { get; set; }
}