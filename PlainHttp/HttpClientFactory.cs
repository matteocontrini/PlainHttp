using System.Collections.Concurrent;
using System.Net;

namespace PlainHttp;

/// <summary>
/// Factory that creates and caches HttpClient.
/// Supports both proxied and non-proxied clients.
/// A similar concept is better explained here by @matteocontrini:
/// https://stackoverflow.com/a/52708837/1633924
/// </summary>
public class HttpClientFactory : IHttpClientFactory
{
    private readonly ConcurrentDictionary<string, HttpClient> clients = new();

    /// <summary>
    /// Gets a cached client for the host associated to the input URL
    /// </summary>
    /// <param name="uri"><see cref="Uri"/> used as the cache key</param>
    /// <returns>A cached <see cref="HttpClient"/> instance for the host</returns>
    public HttpClient GetClient(Uri uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        return PerHostClientFromCache(uri);
    }

    /// <summary>
    /// Gets a random cached client with a proxy attached to it
    /// </summary>
    /// <param name="proxyUri"><see cref="Uri"/> of the proxy, used as the cache key</param>
    /// <returns>A cached <see cref="HttpClient"/> instance with a random proxy. Returns null if no proxies are available</returns>
    public HttpClient GetProxiedClient(Uri proxyUri)
    {
        return ProxiedClientFromCache(proxyUri);
    }

    private HttpClient PerHostClientFromCache(Uri uri)
    {
        return this.clients.AddOrUpdate(
            key: uri.Host,
            addValueFactory: u => CreateClient(),
            updateValueFactory: (u, client) => client);
    }

    private HttpClient ProxiedClientFromCache(Uri proxyUri)
    {
        return this.clients.AddOrUpdate(
            key: proxyUri.ToString(),
            addValueFactory: u => CreateProxiedClient(proxyUri),
            updateValueFactory: (u, client) => client);
    }

    protected virtual HttpClient CreateProxiedClient(Uri proxyUrl)
    {
        WebProxy proxy = new WebProxy(proxyUrl);

        if (!string.IsNullOrEmpty(proxyUrl.UserInfo))
        {
            string[] parts = proxyUrl.UserInfo.Split(':', 2);
            proxy.Credentials = new NetworkCredential(parts[0], parts[1]);
        }

        HttpMessageHandler handler = new SocketsHttpHandler
        {
            Proxy = proxy,
            UseProxy = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        };

        HttpClient client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        return client;
    }

    protected virtual HttpClient CreateClient()
    {
        HttpMessageHandler handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        };

        HttpClient client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        return client;
    }
}
