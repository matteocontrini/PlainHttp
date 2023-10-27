using PlainHttp.Payloads;

namespace PlainHttp;

public interface IHttpRequest
{
    Dictionary<string, string> Headers { get; set; }
    HttpRequestMessage? Message { get; }
    HttpMethod Method { get; set; }
    IPayload? Payload { get; set; }
    Uri? Proxy { get; set; }
    TimeSpan? Timeout { get; set; }
    Uri Uri { get; set; }
    HttpCompletionOption HttpCompletionOption { get; set; }

    Task<IHttpResponse> SendAsync(CancellationToken cancellationToken = default);
}
