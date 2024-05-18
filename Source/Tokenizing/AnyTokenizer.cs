using System.Net.Http;
using System.IO;

namespace LanguageCore.Tokenizing;

public static class AnyTokenizer
{
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="PathTooLongException"/>
    /// <exception cref="DirectoryNotFoundException"/>
    /// <exception cref="UnauthorizedAccessException"/>
    /// <exception cref="FileNotFoundException"/>
    /// <exception cref="System.NotSupportedException"/>
    /// <exception cref="IOException"/>
    /// <exception cref="InternalException"/>
    /// <exception cref="TokenizerException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="System.Threading.Tasks.TaskCanceledException"/>
    /// <exception cref="HttpRequestException"/>
    /// <exception cref="AggregateException"/>
    public static TokenizerResult Tokenize(Uri uri, IEnumerable<string>? preprocessorVariables = null, TokenizerSettings? settings = null)
    {
        if (uri.IsFile)
        { return StreamTokenizer.Tokenize(uri.LocalPath, preprocessorVariables, settings); }
        else
        {
            using HttpClient client = new();
            using HttpResponseMessage res = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result;
            res.EnsureSuccessStatusCode();

            return StreamTokenizer.Tokenize(res.Content.ReadAsStream(), preprocessorVariables, uri, settings);
        }
    }
}
