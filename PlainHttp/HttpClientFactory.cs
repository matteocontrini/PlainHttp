using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
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
        private class HttpClientAndTimestamp
        {
            public HttpClient client;
            public long lastFetchTimestamp; //A return value of Stopwatch.GetTimestamp() to make sure that the timestamp is monotonic.
            public TimeSpan GetElapsedTimeSpan()
            {
                //DateTime.Now is not used because it is not monotonic and it will cause bugs because of that.
                return TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency) - TimeSpan.FromSeconds((double)lastFetchTimestamp / Stopwatch.Frequency);
            }
        }

        /// <summary>
        /// Cache for the clients
        /// </summary>
        private readonly ConcurrentDictionary<string, HttpClientAndTimestamp> clients =
            new ConcurrentDictionary<string, HttpClientAndTimestamp>();

        private TimeSpan clientStaleTimeout;
        private int maximumClientCount;

        /// <summary>
        /// Constructor
        /// <param name="clientStaleTimeout"><see cref="HttpClient"/> instances will be disposed after not being fetched for the specified amount of time</param>
        /// <param name="maximumClientCount">Maximum amount of <see cref="HttpClient"/> instances at a given time. If this amount is exceeded, earliest fetched instances will be deleted</param>
        /// </summary>
        public HttpClientFactory(TimeSpan clientStaleTimeout, int maximumClientCount)
        {
            this.clientStaleTimeout = clientStaleTimeout;
            this.maximumClientCount = maximumClientCount;
        }

        /// <summary>
        /// Disposes old and stale HttpClient instances
        /// </summary>
        private void CleanupHttpClientCache()
        {
            lock (clients)
            {
                foreach (var itemToBeDeleted in clients.Where(x => x.Value.GetElapsedTimeSpan() >= clientStaleTimeout))
                {
                    itemToBeDeleted.Value.client.Dispose();
                    clients.TryRemove(itemToBeDeleted.Key, out _);
                }

                int requiredClientCountToBeDeleted = clients.Count - maximumClientCount;
                if (requiredClientCountToBeDeleted > 0)
                {
                    foreach (var itemToBeDeleted in clients.OrderBy(x => x.Value.lastFetchTimestamp).Take(requiredClientCountToBeDeleted))
                    {
                        itemToBeDeleted.Value.client.Dispose();
                        clients.TryRemove(itemToBeDeleted.Key, out _);
                    }
                }
            }
        }

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
            if (proxy == null)
            {
                throw new ArgumentNullException(nameof(proxy));
            }

            return ProxiedClientFromCache(proxy);
        }

        private HttpClient PerHostClientFromCache(Uri uri)
        {
            lock (clients)
            {
                var result = this.clients.AddOrUpdate(
                    key: uri.Host,
                    addValueFactory: u =>
                    {
                        return new HttpClientAndTimestamp()
                        {
                            client = CreateClient(),
                            lastFetchTimestamp = Stopwatch.GetTimestamp()
                        };
                    },
                    updateValueFactory: (u, value) =>
                    {
                        return value;
                    }
                );
                result.lastFetchTimestamp = Stopwatch.GetTimestamp();
                CleanupHttpClientCache();
                return result.client;
            }
        }

        private HttpClient ProxiedClientFromCache(IWebProxy proxy)
        {
            lock (clients)
            {
                var result = this.clients.AddOrUpdate(
                    key: proxy.GetHashCode().ToString(),
                    addValueFactory: u =>
                    {
                        return new HttpClientAndTimestamp()
                        {
                            client = CreateProxiedClient(proxy),
                            lastFetchTimestamp = Stopwatch.GetTimestamp()
                        };
                    },
                    updateValueFactory: (u, value) =>
                    {
                        return value;
                    }
                );
                result.lastFetchTimestamp = Stopwatch.GetTimestamp();
                CleanupHttpClientCache();
                return result.client;
            }
        }

        private SocketsHttpHandler GetDefaultHttpMessageHandler()
        {
            return new SocketsHttpHandler()
            {
                PooledConnectionLifetime = clientStaleTimeout, //Try to keep the connection open for the lifetime of the HTTP client if no requests are made.
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
