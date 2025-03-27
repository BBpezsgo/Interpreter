using System.IO;
using System.Threading.Tasks;

namespace LanguageCore;

#if UNITY
public delegate UnityEngine.Awaitable<Stream>? FileParser(Uri file);
#else
public delegate Task<Stream>? FileParser(Uri file);
#endif

public class CallbackSourceProvider : ISourceProviderAsync, ISourceQueryProvider
{
    public string? BasePath { get; set; }
    public FileParser FileParser { get; set; }

    public CallbackSourceProvider(FileParser fileParser, string? basePath = null)
    {
        FileParser = fileParser;
        BasePath = basePath;
    }

    public IEnumerable<Uri> GetQuery(string requestedFile, Uri? currentFile)
    {
        if (!requestedFile.EndsWith($".{LanguageConstants.LanguageExtension}", StringComparison.Ordinal)) requestedFile += $".{LanguageConstants.LanguageExtension}";

        if (Uri.TryCreate(currentFile, requestedFile, out Uri? file))
        { yield return file; }

        if (currentFile is not null &&
            BasePath is not null &&
            Uri.TryCreate(new Uri(currentFile, BasePath), requestedFile, out file))
        { yield return file; }

        if (currentFile is not null &&
            !currentFile.IsFile)
        { yield break; }

        if (requestedFile.StartsWith("/~")) requestedFile = requestedFile[1..];
        if (requestedFile.StartsWith('~'))
        { requestedFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + requestedFile[1..]); }

        string? directory = currentFile == null ? null : (new FileInfo(currentFile.AbsolutePath).Directory?.FullName);

        if (directory is not null)
        {
            if (BasePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(BasePath, directory), requestedFile), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(requestedFile, directory), UriKind.Absolute);
        }
        else
        {
            if (BasePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(BasePath, Environment.CurrentDirectory), requestedFile), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(requestedFile, Environment.CurrentDirectory), UriKind.Absolute);
        }
    }

    public SourceProviderResultAsync TryLoad(string requestedFile, Uri? currentFile)
    {
        foreach (Uri file in GetQuery(requestedFile, currentFile))
        {
#pragma warning disable IDE0008 // Use explicit type
            var task = FileParser.Invoke(file);
#pragma warning restore IDE0008

            if (task is null) continue;
            return SourceProviderResultAsync.Success(file, task);
        }

        return SourceProviderResultAsync.NextHandler();
    }
}
