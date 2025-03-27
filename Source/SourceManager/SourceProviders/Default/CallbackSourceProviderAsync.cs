using System.IO;
using System.Threading.Tasks;

namespace LanguageCore;

#if UNITY
public delegate UnityEngine.Awaitable<Stream>? FileParser(Uri file);
#else
public delegate Task<Stream>? FileParser(Uri file);
#endif

public class CallbackSourceProviderAsync : ISourceProviderAsync, ISourceQueryProvider
{
    public string? BasePath { get; set; }
    public FileParser FileParser { get; set; }

    public CallbackSourceProviderAsync(FileParser fileParser, string? basePath = null)
    {
        FileParser = fileParser;
        BasePath = basePath;
    }

    public IEnumerable<Uri> GetQuery(string requestedFile, Uri? currentFile) => new CallbackSourceProviderSync((Func<Uri, Stream?>)((v) => throw new InvalidOperationException()), BasePath).GetQuery(requestedFile, currentFile);

    public SourceProviderResultAsync TryLoad(string requestedFile, Uri? currentFile)
    {
        Uri? lastFile = null;

        foreach (Uri file in GetQuery(requestedFile, currentFile))
        {
            lastFile = file;
#pragma warning disable IDE0008 // Use explicit type
            var task = FileParser.Invoke(file);
#pragma warning restore IDE0008

            if (task is null) continue;
            return SourceProviderResultAsync.Success(file, task);
        }

        if (lastFile is null)
        {
            return SourceProviderResultAsync.NextHandler();
        }
        else
        {
            return SourceProviderResultAsync.NotFound(lastFile);
        }
    }
}
