namespace LatchiXcloud.Core.Updates;

/// <summary>HTTP abstraction so update logic is fully unit-testable without a network.</summary>
public interface IFileSource
{
    /// <summary>GitHub API: latest release info of redphx/better-xcloud as raw JSON.</summary>
    Task<string> GetLatestReleaseJsonAsync();
    /// <summary>Download the userscript bytes from an official release asset URL.</summary>
    Task<byte[]> DownloadBytesAsync(string url);
}

public sealed class HttpFileSource : IFileSource
{
    public static readonly string LatestReleaseApi =
        "https://api.github.com/repos/redphx/better-xcloud/releases/latest";

    private static readonly HttpClient Client = new HttpClient(new SocketsHttpHandler
    {
        UseProxy = false, // direct connection — this app is a client shell, not a proxy
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    static HttpFileSource()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("LATCHI-xCLOUD/0.1.0 (Windows client)");
        Client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<string> GetLatestReleaseJsonAsync() =>
        await Client.GetStringAsync(LatestReleaseApi);

    public async Task<byte[]> DownloadBytesAsync(string url) =>
        await Client.GetByteArrayAsync(url);
}
