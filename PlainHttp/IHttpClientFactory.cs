namespace PlainHttp;

public interface IHttpClientFactory
{
    HttpClient GetClient(Uri requestUri);

    HttpClient GetProxiedClient(Uri requestUri, Uri proxyUri);
}
