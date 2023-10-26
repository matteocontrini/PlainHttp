using System.Diagnostics;
using System.Text;
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

    public string? DownloadFileName { get; set; }

    public Encoding? ResponseEncoding { get; set; }

    public HttpCompletionOption HttpCompletionOption { get; set; } = HttpCompletionOption.ResponseContentRead;

    public bool ReadBody { get; set; } = true;

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
            return await MockedResponse().ConfigureAwait(false);
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

            // Wrap the content into an HttpResponse instance,
            // also reading the body (string or file), if requested
            HttpResponse response = await CreateHttpResponse(responseMessage).ConfigureAwait(false);

            response.ElapsedTime = stopwatch.Elapsed;

            return response;
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestTimeoutException(this, ex);
            }

            throw new HttpRequestException(this, ex);
        }
    }

    private async Task<HttpResponse> CreateHttpResponse(HttpResponseMessage responseMessage)
    {
        // No file name given, read the body of the response as a string
        if (this.DownloadFileName == null)
        {
            HttpResponse response = new HttpResponse(this, responseMessage);

            if (this.ReadBody)
            {
                await response.ReadBody().ConfigureAwait(false);
            }

            return response;
        }
        // Copy the response to a file
        else
        {
            await using (Stream stream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (FileStream fs = new FileStream(this.DownloadFileName, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fs).ConfigureAwait(false);
            }

            return new HttpResponse(this, responseMessage);
        }
    }

    private async Task<IHttpResponse> MockedResponse()
    {
        // Get the testing mode instance for this async context
        HttpResponseMessage message = TestingMode.Value!.RequestsQueue.Dequeue();

        return await CreateHttpResponse(message).ConfigureAwait(false);
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
