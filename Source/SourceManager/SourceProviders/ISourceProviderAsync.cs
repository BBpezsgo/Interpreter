namespace LanguageCore;

public interface ISourceProviderAsync : ISourceProvider
{
    SourceProviderResultAsync TryLoad(string requestedFile, Uri? currentFile);
}
