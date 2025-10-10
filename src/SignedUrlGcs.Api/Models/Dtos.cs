namespace SignedUrlGcs.Api.Models;

public record CreateSignedUrlRequest(
    string? ObjectName,
    int? TtlFromSeconds,
    string? ContentType
);

public record CreateSignedUrlResponse(
    string UploadUrl,
    string ObjectName,
    DateTimeOffset ExpiresAt
);

public record SignedDownloadResponse(
    string DownloadUrl,
    DateTimeOffset ExpiresAt
);
