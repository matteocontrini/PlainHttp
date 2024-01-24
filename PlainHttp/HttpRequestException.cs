namespace PlainHttp;

public class HttpRequestException : Exception
{
    public IHttpRequest Request { get; }
    public IHttpResponse? Response { get; }

    private readonly TimeSpan elapsedTime;
    public TimeSpan ElapsedTime => Response?.ElapsedTime ?? elapsedTime;

    public HttpRequestException(IHttpRequest request, TimeSpan elapsedTime, Exception innerException)
        : base(CreateMessage(request, innerException), innerException)
    {
        this.Request = request;
        this.elapsedTime = elapsedTime;
    }

    public HttpRequestException(IHttpRequest request, IHttpResponse response, Exception innerException)
        : base(CreateMessage(request, innerException), innerException)
    {
        this.Request = request;
        this.Response = response;
    }

    private static string CreateMessage(IHttpRequest request, Exception innerException)
    {
        return $"Failed request: [{request.ToString()}] [{innerException.Message}]";
    }
}
