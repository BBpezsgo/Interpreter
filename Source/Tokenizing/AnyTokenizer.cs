using System.Net.Http;

namespace LanguageCore.Tokenizing;

public static class AnyTokenizer
{
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
