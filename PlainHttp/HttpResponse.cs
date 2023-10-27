using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;

namespace PlainHttp;

public class HttpResponse : IHttpResponse, IDisposable
{
    public HttpRequest Request { get; init; }
    public HttpResponseMessage Message { get; init; }
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

    private Task<T> ReadWrapper<T>(Func<TimeSpan, Task<T>> readFunc)
    {
        Stopwatch? stopwatch = null;
        TimeSpan newTimeout = default;

        // If the HttpCompletionOption is set to ResponseHeadersRead,
        // this method is being called in a second moment, so ElapsedTime is already populated
        if (this.ElapsedTime != default)
        {
            // Start a stopwatch to measure how long the read will last
            stopwatch = Stopwatch.StartNew();

            // Calculate how much time we have left until the timeout
            if (this.Request.Timeout != null)
            {
                newTimeout = this.Request.Timeout.Value - this.ElapsedTime;
            }
        }

        try
        {
            return readFunc(newTimeout);
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

    // TODO: document that it should be disposed
    public Task<Stream> ReadStream()
    {
        // TODO: don't dispose
        return ReadWrapper(async newTimeout =>
            await this.Message.Content
                .ReadAsStreamAsync()
                .WithTimeout(newTimeout)
                .ConfigureAwait(false)
        );
    }

    // TODO: docs
    public Task<string> ReadString()
    {
        // TODO: wrap only if HttpCompletionOption is set to ResponseHeadersRead
        return ReadWrapper(async newTimeout =>
            await this.Message.Content.ReadAsStringAsync()
                .WithTimeout(newTimeout)
                .ConfigureAwait(false)
        );
    }

    public Task<string> ReadString(Encoding encoding)
    {
        // TODO: wrap only if HttpCompletionOption is set to ResponseHeadersRead
        return ReadWrapper(async newTimeout =>
            {
                byte[] array = await this.Message.Content.ReadAsByteArrayAsync()
                    .WithTimeout(newTimeout)
                    .ConfigureAwait(false);

                return encoding.GetString(array);
            }
        );
    }

    public Task<T?> ReadJson<T>(JsonSerializerOptions? options = null)
    {
        // TODO: wrap only if HttpCompletionOption is set to ResponseHeadersRead
        return ReadWrapper(async newTimeout =>
            {
                await using Stream stream = await this.Message.Content
                    .ReadAsStreamAsync()
                    .WithTimeout(newTimeout)
                    .ConfigureAwait(false);

                return JsonSerializer.Deserialize<T>(stream, options);
            }
        );
    }

    public Task<T?> ReadXml<T>(XmlReaderSettings? settings = null)
    {
        // TODO: wrap only if HttpCompletionOption is set to ResponseHeadersRead
        return ReadWrapper(async newTimeout =>
        {
            await using Stream stream = await this.Message.Content
                .ReadAsStreamAsync()
                .WithTimeout(newTimeout)
                .ConfigureAwait(false);

            var reader = XmlReader.Create(stream, settings);
            var serializer = new XmlSerializer(typeof(T));
            return (T?)serializer.Deserialize(reader);
        });
    }

    public Task<string> DownloadFile(string path)
    {
        // TODO: wrap only if HttpCompletionOption is set to ResponseHeadersRead
        return ReadWrapper(async newTimeout =>
        {
            await using Stream stream = await this.Message.Content
                .ReadAsStreamAsync()
                .WithTimeout(newTimeout)
                .ConfigureAwait(false);
            await using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fs).ConfigureAwait(false);
            return path;
        });
    }

    public void EnsureSuccessStatusCode()
    {
        try
        {
            this.Message.EnsureSuccessStatusCode();
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            throw new HttpRequestException(this.Request, ex);
        }
    }

    public void Dispose()
    {
        this.Message.Dispose();
    }
}
