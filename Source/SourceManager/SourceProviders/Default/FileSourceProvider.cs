using System.IO;

namespace LanguageCore;

public class FileSourceProvider : ISourceProviderSync, ISourceQueryProvider
{
    public static readonly FileSourceProvider Instance = new();
    public static string DefaultHomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool AllowLocalFilesFromWeb { get; set; }
    public IEnumerable<string?> ExtraDirectories { get; set; } = Enumerable.Empty<string?>();
    public string? HomeDirectory { get; set; } = DefaultHomeDirectory;

    public IEnumerable<string> GetQuery(string requestedFile, Uri? currentFile)
    {
        if (!requestedFile.EndsWith($".{LanguageConstants.LanguageExtension}", StringComparison.Ordinal))
        {
            requestedFile += $".{LanguageConstants.LanguageExtension}";
        }

        if (requestedFile.StartsWith("/~")) requestedFile = requestedFile[1..];
        if (HomeDirectory is not null && requestedFile.StartsWith('~'))
        {
            requestedFile = Path.Combine(HomeDirectory, "." + requestedFile[1..]);
        }

        if (currentFile is not null)
        {
            foreach (string? extraDirectory in ExtraDirectories)
            {
                if (extraDirectory is null) continue;
                if (!Uri.TryCreate(new Uri(currentFile, extraDirectory), requestedFile, out Uri? file)) continue;
                yield return file.AbsolutePath;
            }
        }

        string? directory = currentFile is null ? null : (new FileInfo(currentFile.AbsolutePath).Directory?.FullName);

        if (directory is not null)
        {
            yield return Path.GetFullPath(requestedFile, directory);

            foreach (string? extraDirectory in ExtraDirectories)
            {
                if (extraDirectory is null) continue;
                yield return Path.Combine(Path.GetFullPath(extraDirectory, directory), requestedFile);
            }
        }

        yield return Path.GetFullPath(requestedFile, Environment.CurrentDirectory);

        foreach (string? extraDirectory in ExtraDirectories)
        {
            if (extraDirectory is null) continue;
            yield return Path.Combine(Path.GetFullPath(extraDirectory, Environment.CurrentDirectory), requestedFile);
        }
    }

    IEnumerable<Uri> ISourceQueryProvider.GetQuery(string requestedFile, Uri? currentFile)
        => GetQuery(requestedFile, currentFile).Select(v => new Uri(v, UriKind.Absolute));

    public SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile)
    {
        if (currentFile is not null && !currentFile.IsFile && !AllowLocalFilesFromWeb)
        {
            return SourceProviderResultSync.NextHandler();
        }

        Uri? lastUri = null;

        foreach (string query in GetQuery(requestedFile, currentFile))
        {
            lastUri = new Uri(query, UriKind.Absolute);

            if (!File.Exists(query)) continue;
            return SourceProviderResultSync.Success(lastUri, File.OpenRead(query));
        }

        if (lastUri is not null)
        {
            return SourceProviderResultSync.NotFound(lastUri!);
        }
        else
        {
            return SourceProviderResultSync.NextHandler();
        }
    }
}
