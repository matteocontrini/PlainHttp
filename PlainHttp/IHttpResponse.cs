using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace PlainHttp;

public interface IHttpResponse : IDisposable
{
    HttpRequest Request { get; }
    HttpResponseMessage Message { get; }
    HttpStatusCode StatusCode { get; }
    bool Succeeded { get; }
    TimeSpan ElapsedTime { get; }

    string? GetSingleHeader(string name);

    /// <summary>
    /// Ensures that the response status code is successful, otherwise throws an exception.
    /// </summary>
    void EnsureSuccessStatusCode();

    /// <summary>
    /// Reads the response body as a stream. The returned stream should be manually disposed after use.
    /// If HttpCompletionOption is set to ResponseHeadersRead, the request timeout will not apply to reads on this stream.
    /// </summary>
    /// <returns>A Task whose result is the response body as a stream.</returns>
    Task<Stream> ReadStream();

    /// <summary>
    /// Reads the response body as a string and disposes the response.
    /// Takes into consideration the timeout if HttpCompletionOption is set to ResponseHeadersRead.
    /// </summary>
    /// <returns>A Task whose result is the response body as a string.</returns>
    Task<string> ReadString();

    /// <summary>
    /// Reads the response body as a string with the given encoding and disposes the response.
    /// Takes into consideration the timeout if HttpCompletionOption is set to ResponseHeadersRead.
    /// </summary>
    /// <param name="encoding">The encoding to use when reading the response body.</param>
    /// <returns>A Task whose result is the response body as a string.</returns>
    Task<string> ReadString(Encoding encoding);

    /// <summary>
    /// Reads and deserializes the response body as JSON and disposes the response.
    /// </summary>
    /// <typeparam name="T">A type whose structure matches the expected JSON response.</typeparam>
    /// <returns>A Task whose result is an object containing data in the response body.</returns> 
    Task<T?> ReadJson<T>(JsonSerializerOptions? options = null);

    /// <summary>
    /// Downloads the response body to the given path and disposes the response.
    /// </summary>
    /// <param name="path">The path to download the file to.</param>
    /// <returns>A Task whose result is the path to the downloaded file.</returns>
    Task<string> DownloadFile(string path);

    Task<T?> ReadXml<T>(XmlReaderSettings? settings = null);

    /// <summary>
    /// Reads the response body as a byte array and disposes the response.
    /// </summary>
    /// <returns>A Task whose result is the response body as a byte array.</returns>
    Task<byte[]> ReadBytes();
}
