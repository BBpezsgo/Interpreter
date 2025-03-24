using System.IO;
using System.Threading.Tasks;

namespace LanguageCore;

public readonly struct SourceProviderResultAsync
{
    public readonly Uri? ResolvedUri;
    public readonly SourceProviderResultType Type;
    public readonly string? ErrorMessage;
#if UNITY
    public readonly UnityEngine.Awaitable<Stream>? Stream;
#else
    public readonly Task<Stream>? Stream;
#endif

    SourceProviderResultAsync(
        SourceProviderResultType type,
        Uri? resolvedUri = null,
#if UNITY
        UnityEngine.Awaitable<Stream>? stream = null,
#else
        Task<Stream>? stream = null,
#endif
        string? errorMessage = null)
    {
        ResolvedUri = resolvedUri;
        ErrorMessage = errorMessage;
        Type = type;
        Stream = stream;
    }

#if UNITY
    public static SourceProviderResultAsync Success(Uri resolvedUri, UnityEngine.Awaitable<Stream> stream) => new(SourceProviderResultType.Success, resolvedUri, stream: stream);
#else
    public static SourceProviderResultAsync Success(Uri resolvedUri, Task<Stream> stream) => new(SourceProviderResultType.Success, resolvedUri, stream: stream);
#endif
    public static SourceProviderResultAsync NotFound(Uri resolvedUri) => new(SourceProviderResultType.NotFound, resolvedUri);
    public static SourceProviderResultAsync NextHandler() => new(SourceProviderResultType.NextHandler);
    public static SourceProviderResultAsync Error(Uri resolvedUri, string? errorMessage = null) => new(SourceProviderResultType.Error, resolvedUri, errorMessage: errorMessage);
}
