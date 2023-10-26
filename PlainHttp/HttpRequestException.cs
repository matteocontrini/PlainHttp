namespace PlainHttp;

public class HttpRequestException : Exception
{
    public HttpRequestException(HttpRequest request, Exception innerException)
        : base(CreateMessage(request, innerException), innerException)
    {
    }

    public HttpRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private static string CreateMessage(HttpRequest request, Exception innerException)
    {
        return $"Failed request: [{request.ToString()}] [{innerException.Message}]";
    }
}
