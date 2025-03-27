using System.IO;

namespace LanguageCore;

public class CallbackSourceProviderSync : ISourceProviderSync, ISourceQueryProvider
{
    public string? BasePath { get; set; }
    public Func<Uri, Stream?> FileParserSync { get; set; }

    static MemoryStream GenerateStreamFromString(string s)
    {
        MemoryStream stream = new();
        StreamWriter writer = new(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    public CallbackSourceProviderSync(Func<Uri, Stream?> fileParser, string? basePath = null)
    {
        FileParserSync = fileParser;
        BasePath = basePath;
    }

    public CallbackSourceProviderSync(Func<Uri, string?> fileParser, string? basePath = null)
    {
        FileParserSync = v =>
        {
            string? content = fileParser.Invoke(v);
            if (content is null) return null;
            return GenerateStreamFromString(content);
        };
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

    public SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile)
    {
        Uri? lastFile = null;

        foreach (Uri file in GetQuery(requestedFile, currentFile))
        {
            lastFile = file;
            Stream? content = FileParserSync.Invoke(file);
            if (content is null) continue;
            return SourceProviderResultSync.Success(file, content);
        }

        if (lastFile is null)
        {
            return SourceProviderResultSync.NextHandler();
        }
        else
        {
            return SourceProviderResultSync.NotFound(lastFile);
        }
    }
}
