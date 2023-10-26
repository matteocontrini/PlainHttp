using System.Diagnostics;

namespace PlainHttp;

public class HttpResponse : IHttpResponse, IDisposable
{
    public HttpRequest Request { get; init; }
    public HttpResponseMessage Message { get; init; }
    public string? Body { get; private set; }
    public bool Succeeded => this.Message.IsSuccessStatusCode;

    public TimeSpan ElapsedTime { get; internal set; }

    public HttpResponse(HttpRequest request, HttpResponseMessage message)
    {
        this.Request = request;
        this.Message = message;
    }

    public string? GetSingleHeader(string name)
    {
        if (this.Message.Headers.TryGetValues(name, out var values) ||
            this.Message.Content.Headers.TryGetValues(name, out values))
        {
            return values.FirstOrDefault();
        }
        else
        {
            return null;
        }
    }

    public async Task ReadBody()
    {
        if (this.Body != null)
        {
            return;
        }

        Stopwatch? stopwatch = null;
        TimeSpan newTimeout = default;

        // If the HttpCompletionOption is set to ResponseHeadersRead,
        // this method is being called in a second moment, so ElapsedTime is already populated
        if (this.ElapsedTime != default)
        {
            // Start a stopwatch to measure how long the read will last
            stopwatch = Stopwatch.StartNew();

            if (this.Request.Timeout != default)
            {
                // Calculate how much time we have left
                newTimeout = this.Request.Timeout.Value - this.ElapsedTime;
            }
        }

        try
        {
            if (this.Request.ResponseEncoding != null)
            {
                byte[] array = await this.Message.Content.ReadAsByteArrayAsync()
                    .WithTimeout(newTimeout)
                    .ConfigureAwait(false);

                this.Body = this.Request.ResponseEncoding.GetString(array);
            }
            else
            {
                this.Body = await this.Message.Content.ReadAsStringAsync()
                    .WithTimeout(newTimeout)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                throw new HttpRequestTimeoutException(this.Request, ex);
            }

            throw new HttpRequestException(this.Request, ex);
        }
        finally
        {
            if (stopwatch != null)
            {
                this.ElapsedTime += stopwatch.Elapsed;
            }

            Dispose();
        }
    }

    public void Dispose()
    {
        this.Message.Dispose();
    }
}
