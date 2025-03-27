namespace LanguageCore;

public interface ISourceQueryProvider
{
    IEnumerable<Uri> GetQuery(string requestedFile, Uri? currentFile);
}

