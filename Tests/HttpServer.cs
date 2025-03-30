
using System.Collections.Frozen;
using System.Net;
using System.Text;

namespace Tests;

public class HttpServer : IDisposable
{
    readonly HttpListener Listener;
    bool IsDisposed;

    public readonly FrozenDictionary<string, string> Routes;
    public int RequestCount { get; private set; }

    public HttpServer(string url, IDictionary<string, string> routes)
    {
        Listener = new HttpListener();
        Listener.Prefixes.Add(url);
        Listener.Start();
        Routes = routes.ToFrozenDictionary();
        Task.Run(HandleIncomingConnections);
    }

    async Task HandleIncomingConnections()
    {
        while (!IsDisposed)
        {
            HttpListenerContext context = await Listener.GetContextAsync();

            RequestCount++;

            if (context.Request.Url is not null &&
                Routes.TryGetValue(context.Request.Url.AbsolutePath, out string? content))
            {
                context.Response.StatusCode = 200;

                byte[] data = Encoding.UTF8.GetBytes(content);
                context.Response.ContentType = "text/plain";
                context.Response.ContentEncoding = Encoding.UTF8;
                context.Response.ContentLength64 = data.LongLength;

                await context.Response.OutputStream.WriteAsync(data);
            }
            else
            {
                context.Response.StatusCode = 404;
            }

            context.Response.Close();
        }

        Listener.Close();
    }

    void Dispose(bool _)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Listener.Close();
    }

    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
}
