namespace PlainHttp;

public class HttpRequestTimeoutException : HttpRequestException
{
    public HttpRequestTimeoutException(HttpRequest request, Exception innerException)
        : base(CreateMessage(request, innerException), innerException)
    {
    }

    private static string CreateMessage(HttpRequest request, Exception innerException)
    {
        return $"Failed request: [{request.ToString()}] [{innerException.Message}]";
    }
}
