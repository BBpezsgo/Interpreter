#if UNITY
using System.IO;
using UnityEngine;

namespace LanguageCore;

public class UnityResourcesSourceProvider : ISourceProviderSync
{
    public string? BaseDirectory { get; set; }

    public SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile)
    {
        if (BaseDirectory is not null)
        {
            requestedFile = Path.Combine(BaseDirectory, requestedFile);
        }

        if (requestedFile.EndsWith(".bbc"))
        {
            requestedFile = $"{requestedFile[..^4]}";
        }

        TextAsset? dataset = Resources.Load<TextAsset>(requestedFile);
        if (dataset == null)
        {
            return SourceProviderResultSync.NotFound(new Uri($"unity://{requestedFile}"));
        }

        return SourceProviderResultSync.Success(new Uri($"unity://{requestedFile}"), dataset.text);
    }
}
#endif
