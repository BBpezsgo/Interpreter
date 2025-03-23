using System.IO;

namespace LanguageCore;

public readonly struct SourceProviderResultSync
{
    public readonly Uri? ResolvedUri;
    public readonly SourceProviderResultType Type;
    public readonly string? ErrorMessage;
    public readonly Stream? Stream;

    SourceProviderResultSync(SourceProviderResultType type, Uri? resolvedUri = null, Stream? stream = null, string? errorMessage = null)
    {
        ResolvedUri = resolvedUri;
        ErrorMessage = errorMessage;
        Type = type;
        Stream = stream;
    }

    public static SourceProviderResultSync Success(Uri resolvedUri, Stream stream) => new(SourceProviderResultType.Success, resolvedUri, stream: stream);
    public static SourceProviderResultSync Success(Uri resolvedUri, byte[] buffer) => new(SourceProviderResultType.Success, resolvedUri, stream: new MemoryStream(buffer));
    public static SourceProviderResultSync Success(Uri resolvedUri, string buffer) => new(SourceProviderResultType.Success, resolvedUri, stream: new MemoryStream(Encoding.UTF8.GetBytes(buffer)));
    public static SourceProviderResultSync NotFound(Uri resolvedUri) => new(SourceProviderResultType.NotFound, resolvedUri);
    public static SourceProviderResultSync NextHandler() => new(SourceProviderResultType.NextHandler);
    public static SourceProviderResultSync Error(Uri resolvedUri, string? errorMessage = null) => new(SourceProviderResultType.Error, resolvedUri, errorMessage: errorMessage);
}
