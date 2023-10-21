using System;
using System.Threading;

namespace TheProgram
{
    internal static class Version
    {
        internal static string Current => Convert(DateTime.Now);
        static DateTime UploadedSavedD;

        internal static string? UploadedSaved { get; private set; }
        internal static string Uploaded
        {
            get
            {
                if (!string.IsNullOrEmpty(UploadedSaved))
                { return UploadedSaved; }

                System.Net.Http.HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");

                System.Net.Http.HttpResponseMessage result = httpClient.GetAsync("https://api.github.com/repos/BBpezsgo/Interpreter/branches/master").Result;
                
                string res = result.Content.ReadAsStringAsync().Result;

                System.Text.Json.JsonDocument resJson = System.Text.Json.JsonDocument.Parse(res);
                string lastCommitDateStr = resJson.RootElement.GetProperty("commit").GetProperty("commit").GetProperty("author").GetProperty("date").GetString()!;
                
                DateTime lastCommitDate = DateTime.Parse(lastCommitDateStr);
                UploadedSaved = Convert(lastCommitDate);
                return UploadedSaved;
            }
        }

        internal static Thread DownloadVersion(Action<string> StateCallback, Action DoneCallback)
        {
            Thread thread = new(new ThreadStart(() =>
            {
                StateCallback?.Invoke("Prepare for download");
                System.Net.Http.HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");

                StateCallback?.Invoke("Send GET request");
                System.Net.Http.HttpResponseMessage result = httpClient.GetAsync("https://api.github.com/repos/BBpezsgo/Interpreter/branches/master").Result;

                StateCallback?.Invoke("Download content");
                string res = result.Content.ReadAsStringAsync().Result;

                StateCallback?.Invoke("Parse content");
                System.Text.Json.JsonDocument resJson = System.Text.Json.JsonDocument.Parse(res);
                string lastCommitDateStr = resJson.RootElement.GetProperty("commit").GetProperty("commit").GetProperty("author").GetProperty("date").GetString()!;

                DateTime lastCommitDate = DateTime.Parse(lastCommitDateStr);
                UploadedSavedD = lastCommitDate;
                UploadedSaved = Convert(lastCommitDate);

                DoneCallback?.Invoke();
            }));
            thread.Start();
            return thread;
        }

        internal static string Convert(DateTime d) => $"{d.Year}.{d.Month}.{d.Day}";

        internal static bool HasNewVersion()
        {
            var Current = DateTime.Now;

            if (Current.Year < UploadedSavedD.Year) return true;
            if (Current.Month < UploadedSavedD.Month) return true;
            if (Current.Day < UploadedSavedD.Day) return true;
            return false;
        }
    }
}
