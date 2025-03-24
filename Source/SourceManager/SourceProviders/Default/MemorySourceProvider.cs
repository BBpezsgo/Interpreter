namespace LanguageCore;

public class MemorySourceProvider : ISourceProviderSync
{
    readonly FrozenDictionary<string, string> Sources;

    public MemorySourceProvider(IDictionary<string, string> sources)
    {
        Sources = sources.ToFrozenDictionary();
    }

    public SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile)
    {
        Uri uri = currentFile is not null ? new Uri(currentFile, requestedFile) : new(requestedFile);
        if (Sources.TryGetValue(requestedFile, out string? content))
        {
            return SourceProviderResultSync.Success(uri, content);
        }

        return SourceProviderResultSync.NotFound(uri);
    }
}
