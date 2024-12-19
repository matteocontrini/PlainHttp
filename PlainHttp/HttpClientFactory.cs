using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;

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
    private readonly HttpHandlerOptions handlerOptions;
    private readonly HttpHandlerOptions proxyHandlerOptions;

    public record HttpHandlerOptions
    {
        /// <summary>
        /// The maximum lifetime of a connection in the pool. Default: 10 minutes.
        /// </summary>
        public TimeSpan PooledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The maximum idle time of a connection in the pool. If a connection is idle for more than this time, it will be closed. Default: 1 minute.
        /// </summary>
        public TimeSpan PooledConnectionIdleTimeout { get; init; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The timeout for establishing a connection to the server. Default: infinite.
        /// </summary>
        public TimeSpan ConnectTimeout { get; init; } = Timeout.InfiniteTimeSpan;

        /// <summary>
        /// The decompression methods to use for the response body. By default, all methods (gzip, DEFLATE and Brotli) are enabled.
        /// </summary>
        public DecompressionMethods AutomaticDecompression { get; init; } = DecompressionMethods.All;

        /// <summary>
        /// The SSL/TLS protocols to use. By default, the system default is used.
        /// </summary>
        public SslProtocols EnabledSslProtocols { get; init; } = SslProtocols.None;

        /// <summary>
        /// Whether to ignore certificate validation errors. Default: false.
        /// </summary>
        public bool IgnoreCertificateValidationErrors { get; init; }

        /// <summary>
        /// Whether redirect responses should be automatically followed. Default: true.
        /// </summary>
        public bool AllowAutoRedirect { get; set; } = true;
    }

    public HttpClientFactory()
    {
        this.handlerOptions = new HttpHandlerOptions();
        this.proxyHandlerOptions = new HttpHandlerOptions();
    }

    public HttpClientFactory(HttpHandlerOptions handlerOptions)
    {
        this.handlerOptions = handlerOptions;
        this.proxyHandlerOptions = handlerOptions;
    }

    public HttpClientFactory(HttpHandlerOptions handlerOptions, HttpHandlerOptions proxyHandlerOptions)
    {
        this.handlerOptions = handlerOptions;
        this.proxyHandlerOptions = proxyHandlerOptions;
    }

    /// <summary>
    /// Gets a cached client for the host associated to the provided request URI.
    /// </summary>
    /// <param name="requestUri">Request <see cref="Uri"/> used as the cache key</param>
    /// <returns>A cached <see cref="HttpClient"/> instance for the host</returns>
    public HttpClient GetClient(Uri requestUri)
    {
        return PerHostClientFromCache(requestUri);
    }

    /// <summary>
    /// Gets a cached client with the given proxy attached to it.
    /// </summary>
    /// <param name="requestUri">Request <see cref="Uri"/></param>
    /// <param name="proxyUri"><see cref="Uri"/> of the proxy, used as the cache key</param>
    /// <returns>A cached <see cref="HttpClient"/> instance with the proxy configured</returns>
    public HttpClient GetProxiedClient(Uri requestUri, Uri proxyUri)
    {
        return ProxiedClientFromCache(proxyUri);
    }

    private HttpClient PerHostClientFromCache(Uri uri)
    {
        return this.clients.AddOrUpdate(
            key: uri.Host,
            addValueFactory: u => CreateClient(),
            updateValueFactory: (u, client) => client
        );
    }

    private HttpClient ProxiedClientFromCache(Uri proxyUri)
    {
        return this.clients.AddOrUpdate(
            key: proxyUri.ToString(),
            addValueFactory: u => CreateProxiedClient(proxyUri),
            updateValueFactory: (u, client) => client
        );
    }

    protected virtual HttpClient CreateClient()
    {
        HttpMessageHandler handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = this.handlerOptions.PooledConnectionLifetime,
            PooledConnectionIdleTimeout = this.handlerOptions.PooledConnectionIdleTimeout,
            UseCookies = false,
            AutomaticDecompression = this.handlerOptions.AutomaticDecompression,
            SslOptions = CreateSslOptions(
                this.handlerOptions.EnabledSslProtocols,
                this.handlerOptions.IgnoreCertificateValidationErrors
            ),
            ConnectTimeout = this.handlerOptions.ConnectTimeout,
            AllowAutoRedirect = this.handlerOptions.AllowAutoRedirect,
        };

        HttpClient client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        return client;
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
            PooledConnectionLifetime = this.proxyHandlerOptions.PooledConnectionLifetime,
            PooledConnectionIdleTimeout = this.proxyHandlerOptions.PooledConnectionIdleTimeout,
            UseCookies = false,
            AutomaticDecompression = this.proxyHandlerOptions.AutomaticDecompression,
            SslOptions = CreateSslOptions(
                this.proxyHandlerOptions.EnabledSslProtocols,
                this.proxyHandlerOptions.IgnoreCertificateValidationErrors
            ),
            ConnectTimeout = this.proxyHandlerOptions.ConnectTimeout,
            AllowAutoRedirect = this.proxyHandlerOptions.AllowAutoRedirect,
        };

        HttpClient client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        return client;
    }

    protected virtual SslClientAuthenticationOptions CreateSslOptions(
        SslProtocols sslProtocols,
        bool ignoreCertificateValidationErrors)
    {
        return new SslClientAuthenticationOptions
        {
            EnabledSslProtocols = sslProtocols,
            RemoteCertificateValidationCallback = ignoreCertificateValidationErrors
                ? (sender, certificate, chain, errors) => true
                : null
        };
    }
}
