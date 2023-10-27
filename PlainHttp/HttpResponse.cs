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
            timeLeft = this.Request.Timeout != null
                ? this.Request.Timeout.Value - this.ElapsedTime
                : default;

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

    /// <summary>
    /// Reads the response body as a stream. The returned stream should be manually disposed after use.
    /// If HttpCompletionOption is set to ResponseHeadersRead, the request timeout will not apply to reads on this stream.
    /// </summary>
    /// <returns>A Task whose result is the response body as a stream.</returns>
    public async Task<Stream> ReadStream()
    {
        return await this.Message.Content
            .ReadAsStreamAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the response body as a string and disposes the response.
    /// Takes into consideration the timeout if HttpCompletionOption is set to ResponseHeadersRead.
    /// </summary>
    /// <returns>A Task whose result is the response body as a string.</returns>
    public async Task<string> ReadString()
    {
        return await ReadWrapper(timeLeft =>
            this.Message.Content
                .ReadAsStringAsync()
                .WaitAsync(timeLeft)
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the response body as a string with the given encoding and disposes the response.
    /// Takes into consideration the timeout if HttpCompletionOption is set to ResponseHeadersRead.
    /// </summary>
    /// <param name="encoding">The encoding to use when reading the response body.</param>
    /// <returns>A Task whose result is the response body as a string.</returns>
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

    /// <summary>
    /// Reads and deserializes the response body as JSON and disposes the response.
    /// </summary>
    /// <typeparam name="T">A type whose structure matches the expected JSON response.</typeparam>
    /// <returns>A Task whose result is an object containing data in the response body.</returns> 
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

    /// <summary>
    /// Downloads the response body to the given path and disposes the response.
    /// </summary>
    /// <param name="path">The path to download the file to.</param>
    /// <returns>A Task whose result is the path to the downloaded file.</returns>
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

    /// <summary>
    /// Reads the response body as a byte array and disposes the response.
    /// </summary>
    /// <returns>A Task whose result is the response body as a byte array.</returns>
    public async Task<byte[]> ReadBytes()
    {
        return await ReadWrapper(timeLeft =>
            this.Message.Content
                .ReadAsByteArrayAsync()
                .WaitAsync(timeLeft)
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures that the response status code is successful, otherwise throws an exception.
    /// </summary>
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
