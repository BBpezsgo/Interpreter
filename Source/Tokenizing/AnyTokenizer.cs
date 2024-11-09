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
    /// <exception cref="InternalExceptionWithoutContext"/>
    /// <exception cref="TokenizerException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="System.Threading.Tasks.TaskCanceledException"/>
    /// <exception cref="HttpRequestException"/>
    /// <exception cref="AggregateException"/>
    public static TokenizerResult Tokenize(Uri file, DiagnosticsCollection diagnostics, IEnumerable<string>? preprocessorVariables = null, TokenizerSettings? settings = null)
    {
        if (file.IsFile)
        { return StreamTokenizer.Tokenize(file.AbsolutePath, diagnostics, preprocessorVariables, settings); }
        else
        {
            using HttpClient client = new();
            using HttpResponseMessage res = client.GetAsync(file, HttpCompletionOption.ResponseHeadersRead).Result;
            res.EnsureSuccessStatusCode();

            return StreamTokenizer.Tokenize(res.Content.ReadAsStream(), diagnostics, preprocessorVariables, file, settings);
        }
    }
}
