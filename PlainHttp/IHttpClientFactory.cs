namespace PlainHttp;

public interface IHttpClientFactory
{
    HttpClient GetClient(Uri uri);

    HttpClient GetProxiedClient(Uri proxyUri);
}
