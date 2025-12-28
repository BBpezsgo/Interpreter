namespace LanguageCore;

public class MemorySourceProvider : ISourceProviderSync, IVersionProvider
{
    readonly FrozenDictionary<string, string> Sources;

    public MemorySourceProvider(IDictionary<string, string> sources)
    {
        Sources = sources.ToFrozenDictionary();
    }

    public SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile)
    {
        if (!Uri.TryCreate(currentFile, requestedFile, out Uri? uri) ||
            uri.Scheme != "memory")
        {
            return SourceProviderResultSync.NextHandler();
        }

        if (Sources.TryGetValue(uri.LocalPath, out string? content))
        {
            return SourceProviderResultSync.Success(uri, content);
        }
        else
        {
            return SourceProviderResultSync.NotFound(uri);
        }
    }

    public bool TryGetVersion(Uri uri, out ulong version)
    {
        version = 1;
        return uri.Scheme is "memory" && Sources.ContainsKey(uri.LocalPath);
    }
}
