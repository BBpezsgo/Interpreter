
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LanguageCore;

public class HttpSourceProvider : ISourceProviderAsync, ISourceQueryProvider
{
    public static readonly HttpSourceProvider Instance = new();

    public IEnumerable<Uri> GetQuery(string requestedFile, Uri? currentFile)
    {
        if (!requestedFile.EndsWith($".{LanguageConstants.LanguageExtension}", StringComparison.Ordinal))
        {
            requestedFile += $".{LanguageConstants.LanguageExtension}";
        }

        if (!Uri.TryCreate(currentFile, requestedFile, out Uri? file))
        {
            yield break;
        }

        if (file.Scheme is not "https" and not "http")
        {
            yield break;
        }

        yield return file;
    }

    public SourceProviderResultAsync TryLoad(string requestedFile, Uri? currentFile)
    {
        foreach (Uri file in GetQuery(requestedFile, currentFile))
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"BBLang Compiler Source Collector");
            using Task<HttpResponseMessage> getTask = client.GetAsync(file);

            try
            {
                getTask.Wait();
            }
            catch (AggregateException ex)
            {
                foreach (Exception error in ex.InnerExceptions)
                { Output.LogError(error.Message); }

                return SourceProviderResultAsync.Error(file, ex.InnerException?.Message);
            }

            HttpResponseMessage res = getTask.Result;

            if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return SourceProviderResultAsync.NotFound(file);
            }

            if (!res.IsSuccessStatusCode)
            {
                return SourceProviderResultAsync.Error(file, res.StatusCode.ToString());
            }

#if UNITY
            throw new System.NotSupportedException($"Unity not supported");
#else
            return SourceProviderResultAsync.Success(file, Task.Run<Stream>(async () =>
            {
                using Stream stream = await res.Content.ReadAsStreamAsync();
                using StreamReader reader = new(stream);
                string content = await reader.ReadToEndAsync();
                res.Dispose();
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
            }));
#endif
        }

        return SourceProviderResultAsync.NextHandler();
    }
}
