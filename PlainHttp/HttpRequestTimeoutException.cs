namespace PlainHttp;

public class HttpRequestTimeoutException : HttpRequestException
{
    public HttpRequestTimeoutException(IHttpRequest request, TimeSpan elapsedTime, Exception innerException)
        : base(request, elapsedTime, innerException)
    {
    }

    public HttpRequestTimeoutException(IHttpRequest request, IHttpResponse response, Exception innerException)
        : base(request, response, innerException)
    {
    }
}
