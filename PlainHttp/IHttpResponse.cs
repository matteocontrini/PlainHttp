﻿using System.Text;
using System.Text.Json;
using System.Xml;

namespace PlainHttp;

public interface IHttpResponse
{
    HttpRequest Request { get; }
    HttpResponseMessage Message { get; }
    bool Succeeded { get; }
    TimeSpan ElapsedTime { get; }

    string? GetSingleHeader(string name);
    void EnsureSuccessStatusCode();
    Task<string> ReadString();
    Task<string> ReadString(Encoding encoding);
    Task<Stream> ReadStream();
    Task<T?> ReadJson<T>(JsonSerializerOptions? options = null);
    Task<string> DownloadFile(string path);
    Task<T?> ReadXml<T>(XmlReaderSettings? settings = null);

    /// <summary>
    /// Reads the response body as a byte array and disposes the response.
    /// </summary>
    /// <returns>A Task whose result is the response body as a byte array.</returns>
    Task<byte[]> ReadBytes();
}
