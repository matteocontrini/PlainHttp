using System.Diagnostics;
using PlainHttp.Payloads;

namespace PlainHttp;

public class HttpRequest : IHttpRequest
{
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    public Uri Uri { get; set; }

    public Version? HttpVersion { get; set; }

    public static IHttpClientFactory HttpClientFactory { get; set; } = new HttpClientFactory();

    public HttpRequestMessage? Message { get; protected set; }

    public TimeSpan? Timeout { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public Uri? Proxy { get; set; }

    public IPayload? Payload { get; set; }

    public HttpCompletionOption HttpCompletionOption { get; set; } = HttpCompletionOption.ResponseContentRead;

    private static readonly AsyncLocal<TestingMode> TestingMode = new();

    public HttpRequest(Uri uri)
    {
        this.Uri = uri;
    }

    public HttpRequest(string url)
    {
        this.Uri = new Uri(url);
    }

    public async Task<IHttpResponse> SendAsync(CancellationToken cancellationToken = default)
    {
        if (TestingMode.Value != null)
        {
            return MockedResponse();
        }

        HttpClient client = this.Proxy != null
            ? HttpClientFactory.GetProxiedClient(this.Uri, this.Proxy)
            : HttpClientFactory.GetClient(this.Uri);

        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            Method = this.Method,
            RequestUri = this.Uri
        };

        // Add the headers to the request
        foreach (string headerName in this.Headers.Keys)
        {
            requestMessage.Headers.TryAddWithoutValidation(headerName, this.Headers[headerName]);
        }

        // Set the HTTP version
        if (this.HttpVersion != null)
        {
            requestMessage.Version = this.HttpVersion;
        }

        // Save the HttpRequestMessage
        this.Message = requestMessage;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Enable timeout, if set
        if (this.Timeout != null)
        {
            cts.CancelAfter(this.Timeout.Value);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Serialize the payload
            if (this.Payload != null)
            {
                requestMessage.Content = this.Payload.Serialize();
            }

            // Send the request
            HttpResponseMessage responseMessage = await client
                .SendAsync(requestMessage, this.HttpCompletionOption, cts.Token)
                .ConfigureAwait(false);

            // Wrap the content into an HttpResponse instance
            HttpResponse response = new HttpResponse(this, responseMessage);
            response.ElapsedTime = stopwatch.Elapsed;

            return response;
        }
        catch (Exception ex)
        {
            // Covers both TaskCanceledException and OperationCanceledException
            if (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestTimeoutException(this, ex);
            }

            throw new HttpRequestException(this, ex);
        }
    }

    private IHttpResponse MockedResponse()
    {
        // Get the testing mode instance for this async context
        HttpResponseMessage message = TestingMode.Value!.RequestsQueue.Dequeue();
        return new HttpResponse(this, message);
    }

    public override string ToString()
    {
        return $"{this.Method} {this.Uri}";
    }

    public static void SetTestingMode(TestingMode t)
    {
        TestingMode.Value = t;
    }
}
