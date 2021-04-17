using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;

namespace PlainHttp
{
    /// <summary>
    /// Factory that creates and caches HttpClient.
    /// Supports both proxied and non-proxied clients.
    /// A similar concept is better explained here by @matteocontrini:
    /// https://stackoverflow.com/a/52708837/1633924
    /// </summary>
    public class HttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        /// Cache for the clients
        /// </summary>
        private readonly ConcurrentDictionary<string, HttpClient> clients =
            new ConcurrentDictionary<string, HttpClient>();

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
        /// <param name="proxy"><see cref="IWebProxy"/> of the proxy, whose hash code is used as the cache key</param>
        /// <returns>A cached <see cref="HttpClient"/> instance with a random proxy. Returns null if no proxies are available</returns>
        public HttpClient GetProxiedClient(IWebProxy proxy)
        {
            return ProxiedClientFromCache(proxy);
        }

        private HttpClient PerHostClientFromCache(Uri uri)
        {
            return this.clients.AddOrUpdate(
                key: uri.Host,
                addValueFactory: u =>
                {
                    return CreateClient();
                },
                updateValueFactory: (u, client) =>
                {
                    return client;
                }
            );
        }

        private HttpClient ProxiedClientFromCache(IWebProxy proxy)
        {
            return this.clients.AddOrUpdate(
                key: proxy.GetHashCode().ToString(),
                addValueFactory: u =>
                {
                    return CreateProxiedClient(proxy);
                },
                updateValueFactory: (u, client) =>
                {
                    return client;
                }
            );
        }

        private SocketsHttpHandler GetDefaultHttpMessageHandler()
        {
            return new SocketsHttpHandler()
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };
        }

        protected virtual HttpClient CreateProxiedClient(IWebProxy proxy)
        {
            var handler = GetDefaultHttpMessageHandler();
            handler.Proxy = proxy;
            handler.UseProxy = true;

            HttpClient client = new HttpClient(handler)
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };

            return client;
        }

        protected virtual HttpClient CreateClient()
        {
            HttpClient client = new HttpClient(GetDefaultHttpMessageHandler())
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };

            return client;
        }
    }
}
