﻿using System;
using System.Globalization;
using System.Threading;

namespace LanguageCore;

public static class Version
{
    public static string Current => Convert(DateTime.Now);
    static DateTime UploadedSavedD;

    static Uri ProjectGithubUri => new("https://api.github.com/repos/BBpezsgo/Interpreter/branches/master");
    const string Cookies = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36";

    public static string? UploadedSaved { get; private set; }
    public static string Uploaded
    {
        get
        {
            if (!string.IsNullOrEmpty(UploadedSaved))
            { return UploadedSaved; }

            using System.Net.Http.HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Cookies);

            System.Net.Http.HttpResponseMessage result = httpClient.GetAsync(ProjectGithubUri).Result;

            string res = result.Content.ReadAsStringAsync().Result;

            System.Text.Json.JsonDocument resJson = System.Text.Json.JsonDocument.Parse(res);
            string lastCommitDateStr = resJson.RootElement.GetProperty("commit").GetProperty("commit").GetProperty("author").GetProperty("date").GetString()!;

            DateTime lastCommitDate = DateTime.Parse(lastCommitDateStr, CultureInfo.CurrentCulture);
            UploadedSaved = Convert(lastCommitDate);
            return UploadedSaved;
        }
    }

    public static Thread DownloadVersion(Action<string> StateCallback, Action DoneCallback)
    {
        Thread thread = new(new ThreadStart(() =>
        {
            StateCallback?.Invoke("Prepare for download");
            System.Net.Http.HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Cookies);

            StateCallback?.Invoke("Send GET request");
            System.Net.Http.HttpResponseMessage result = httpClient.GetAsync(ProjectGithubUri).Result;

            StateCallback?.Invoke("Download content");
            string res = result.Content.ReadAsStringAsync().Result;

            StateCallback?.Invoke("Parse content");
            System.Text.Json.JsonDocument resJson = System.Text.Json.JsonDocument.Parse(res);
            string lastCommitDateStr = resJson.RootElement.GetProperty("commit").GetProperty("commit").GetProperty("author").GetProperty("date").GetString()!;

            DateTime lastCommitDate = DateTime.Parse(lastCommitDateStr, CultureInfo.CurrentCulture);
            UploadedSavedD = lastCommitDate;
            UploadedSaved = Convert(lastCommitDate);

            DoneCallback?.Invoke();
        }));
        thread.Start();
        return thread;
    }

    public static string Convert(DateTime d) => $"{d.Year}.{d.Month}.{d.Day}";

    public static bool HasNewVersion()
    {
        DateTime Current = DateTime.Now;

        if (Current.Year < UploadedSavedD.Year) return true;
        if (Current.Month < UploadedSavedD.Month) return true;
        if (Current.Day < UploadedSavedD.Day) return true;
        return false;
    }
}
