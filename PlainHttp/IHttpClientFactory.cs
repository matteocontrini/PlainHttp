using System;
using System.Net;
using System.Net.Http;

namespace PlainHttp
{
    public interface IHttpClientFactory
    {
        HttpClient GetClient(Uri uri);

        HttpClient GetProxiedClient(IWebProxy proxy);
    }
}
