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

    private async Task<T> ReadWrapper<T>(Func<TimeSpan, Task<T>> readFunc)
    {
        Stopwatch? stopwatch = null;
        TimeSpan timeLeft = default;

        if (this.Request.HttpCompletionOption == HttpCompletionOption.ResponseHeadersRead)
        {
            // Calculate how much time we have left until the timeout
            timeLeft = this.Request.Timeout != Timeout.InfiniteTimeSpan
                ? this.Request.Timeout - this.ElapsedTime
                : Timeout.InfiniteTimeSpan;

            // Start measuring how long the read will last
            stopwatch = Stopwatch.StartNew();
        }

        try
        {
            return await readFunc(timeLeft).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is TimeoutException)
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

    /// <inheritdoc />
    public async Task<Stream> ReadStream()
    {
        return await this.Message.Content
            .ReadAsStreamAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> ReadString()
    {
        return await ReadWrapper(timeLeft =>
            this.Message.Content
                .ReadAsStringAsync()
                .WaitAsync(timeLeft)
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> ReadString(Encoding encoding)
    {
        return await ReadWrapper(async timeLeft =>
            {
                byte[] array = await this.Message.Content.ReadAsByteArrayAsync()
                    .WaitAsync(timeLeft)
                    .ConfigureAwait(false);

                return encoding.GetString(array);
            }
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> ReadJson<T>(JsonSerializerOptions? options = null)
    {
        return await ReadWrapper(async timeLeft =>
            {
                await using Stream stream = await this.Message.Content
                    .ReadAsStreamAsync()
                    .ConfigureAwait(false);

                if (this.Request.HttpCompletionOption == HttpCompletionOption.ResponseHeadersRead)
                {
                    // Use async variant since the deserializer will do the actual read
                    // from the network, through the stream
                    return await JsonSerializer.DeserializeAsync<T>(stream, options)
                        .AsTask()
                        .WaitAsync(timeLeft)
                        .ConfigureAwait(false);
                }
                else
                {
                    // The stream is a fully-read MemoryStream, so we can use the sync variant
                    // (it would be sync even if we used the async variant)
                    // https://github.com/dotnet/runtime/issues/1574#issuecomment-535324331
                    return JsonSerializer.Deserialize<T>(stream, options);
                }
            }
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> ReadXml<T>(XmlReaderSettings? settings = null)
    {
        return await ReadWrapper(async timeLeft =>
        {
            await using Stream stream = await this.Message.Content
                .ReadAsStreamAsync()
                .ConfigureAwait(false);

            // TODO: this is synchronous unfortunately so we're not setting the timeout
            var reader = XmlReader.Create(stream, settings);
            var serializer = new XmlSerializer(typeof(T));
            return (T?)serializer.Deserialize(reader);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> DownloadFile(string path)
    {
        return await ReadWrapper(async timeLeft =>
        {
            await using Stream stream = await this.Message.Content
                .ReadAsStreamAsync()
                .ConfigureAwait(false);

            await using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            await stream.CopyToAsync(fs)
                .WaitAsync(timeLeft)
                .ConfigureAwait(false);

            return path;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]> ReadBytes()
    {
        return await ReadWrapper(timeLeft =>
            this.Message.Content
                .ReadAsByteArrayAsync()
                .WaitAsync(timeLeft)
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
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
