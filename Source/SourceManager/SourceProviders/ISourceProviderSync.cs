namespace LanguageCore;

public interface ISourceProviderSync : ISourceProvider
{
    SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile);
}
