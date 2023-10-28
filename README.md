# PlainHttp [![NuGet](https://img.shields.io/nuget/v/PlainHttp?color=success)](https://www.nuget.org/packages/PlainHttp) [![License](https://img.shields.io/github/license/matteocontrini/PlainHttp?color=success)](https://github.com/matteocontrini/PlainHttp/blob/master/LICENSE)

An **easy HTTP client** for .NET 6+ with support for **serialization, deserialization, proxies, testing**, and more.

## Features

- Wraps `HttpClient` and provides a cleaner and easier interface
- Supports any HTTP method
- Per-request timeout with an actual `HttpRequestTimeoutException`
- Per-request proxy with transparent pooling
- Built-in serialization of objects to `JSON`/`XML`/URL-encoded, extensible to any other format
- Built-in deserialization of `JSON`/`XML` responses
- Download files to disk
- Read responses with specific response encodings
- Automatically enabled decompression of responses (all algorithms supported by .NET, i.e. gzip, DEFLATE, and Brotli)
- Proper pooling and connection lifetime defaults to avoid DNS and socket exhaustion issues  
- Allows to mock requests for unit testing
- Heavily used in production by [@trackbotpro](https://github.com/trackbotpro/) to send millions of requests per day

## Supported frameworks

This library targets .NET 6 (LTS) because it requires the `PooledConnectionLifetime` property on `SocketsHttpHandler`, introduced in .NET Core 2.2.

This makes sure that reusing the same `HttpClient` for a long time doesn't have [unintended consequences](https://github.com/dotnet/corefx/issues/11224) affecting DNS resolution. This library in fact keeps a pool of `HttpClient` instances that are never disposed.

In particular, the library keeps:

- One `HttpClient` per request host
- One `HttpClient` per proxy URI (including credentials)

There is currently no mechanism that disposes `HttpClient` instances that are unused, so if you use a lot of random proxies or many different hostnames, you might get into trouble. This can be easily improved by creating a custom [`IHttpClientFactory`](https://github.com/matteocontrini/PlainHttp/blob/master/PlainHttp/HttpClientFactory.cs), and then setting the factory instance to the static `HttpClientFactory` property.

## Installation

Install the [PlainHttp](https://www.nuget.org/packages/PlainHttp) NuGet package:

```
dotnet add package PlainHttp
```

### Upgrading from 1.x to 2.x

See the release notes for [v2.0.0](https://github.com/matteocontrini/PlainHttp/releases/tag/v2.0.0).

## Usage

- [Basic usage](#basic-usage)
- [Error handling](#error-handling)
- [Request customization](#request-customization)
- [Request serialization](#request-serialization)
- [Response deserialization](#response-deserialization)
- [Efficiently reading the response body](#efficiently-reading-the-response-body)
- [Downloading files](#downloading-files)
- [Proxies](#proxies)
- [URL building](#url-building)
- [Testing mode](#testing-mode)
- [Custom serialization](#custom-serialization)
- [Customizing `HttpClient` defaults](#customizing-httpclient-defaults)

### Basic usage

Basic `GET` request:

```c#
string url = "http://random.org";
IHttpRequest request = new HttpRequest(url);
IHttpResponse response = await request.SendAsync();
string body = await response.ReadString();
```

Also with `Uri`:

```c#
Uri uri = new Uri("http://random.org");
IHttpRequest request = new HttpRequest(uri);
```

### Error handling

Checking if the HTTP status code is in the `2xx` range:

```c#
IHttpResponse response = await request.SendAsync();

if (!response.Succeeded)
{
    Console.WriteLine($"Response status code is {response.StatusCode}");
}
else
{
    Console.WriteLine($"Successful response in {response.ElapsedMilliseconds} ms");
}
```

Asserting that the HTTP status code is in the `2xx` range:

```c#
IHttpResponse response = await request.SendAsync();
response.EnsureSuccessStatusCode(); // may throw HttpRequestException
```

Every exception is wrapped in an `HttpRequestException`, from which `HttpRequestTimeoutException` is derived:

```c#
try
{
    IHttpResponse response = await request.SendAsync();
}
catch (HttpRequestException ex)
{
    if (ex is HttpRequestTimeoutException)
    {
        Console.WriteLine("Request timed out");
    }
    else
    {
        Console.WriteLine("Something bad happened: {0}", ex);
        // PlainHttp.HttpRequestException: Failed request: [GET https://yyyy.org/] [No such host is known] ---> System.Net.Http.HttpRequestException: No such host is known ---> System.Net.Sockets.SocketException: No such host is known
        // etc.
    }
}
```

### Request customization

Setting **custom headers**:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Headers = new Dictionary<string, string>
    {
        // No user agent is set by default
        { "User-Agent", "PlainHttp/1.0" }
    }
};
```

Request a **specific HTTP version** to be used. If it's not supported, the default [`HttpVersionPolicy`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpversionpolicy) applies (downgrade to a lower version).

```csharp
IHttpRequest request = new HttpRequest(url)
{
    Version = new Version(2, 0) // HTTP/2
};
```

**Custom timeout** (by default no timeout is set):

```c#
IHttpRequest request = new HttpRequest(url)
{
    Timeout = TimeSpan.FromSeconds(10)
};
```

### Request serialization

`POST` request with **URL-encoded payload**:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    Payload = new FormUrlEncodedPayload(new
    {
        hello = "world",
        buuu = true
    })
};
```

`POST` request with **JSON payload** (powered by `System.Text.Json`):

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    Payload = new JsonPayload(new
    {
        hello = "world"
    })
};
```

You can pass `JsonSerializerOptions` with the second argument of `JsonPayload`.

If you already have a JSON-serialized string, just pass it directly:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    Payload = new JsonPayload("{ \"key\": true }")
};
```

`POST` request with **XML payload** (powered by `System.Xml.Serialization.XmlSerializer`):

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    Payload = new XmlPayload(new
    {
        something = "web"
    })
};
```

You can pass XML serialization options with the second argument of `XmlPayload` (`XmlWriterSettings`). If you already have an XML-serialized string, just pass it directly.

`POST` request with **plain text payload**:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    Payload = new PlainTextPayload("plain text")
};
```

### Response deserialization

To read the response body **as a string**:

```c#
string body = await response.ReadString();
```

Optionally, you can specify the **encoding** to use:

```c#
string body = await response.ReadString(Encoding.GetEncoding("ISO-8859-1"));
```

To read the body **as a stream**:

```c#
Stream stream = await response.ReadStream();
```

Note that when using `ReadStream` the response message is not automatically disposed, so you must take care of disposing it manually when you're done with it.

To deserialize the response **as JSON**:

```c#
ResponseDTO content = await response.ReadJson<ResponseDTO>();
```

To deserialize the response **as XML**:

```c#
ResponseDTO content = await response.ReadXml<ResponseDTO>();
```

To read the body **as a byte array**:

```c#
byte[] bytes = await response.ReadBytes();
```

### Efficiently reading the response body

By default, the full response body is loaded in memory during the `SendAsync` call. This means that when calling the various `Read*` methods, the response body is already fully downloaded and is thereefore read from a memory stream.

To change this, you can set the `HttpCompletionOption` request option to `HttpCompletionOption.ResponseHeadersRead` (from `System.Net.Http`):

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Get,
    HttpCompletionOption = HttpCompletionOption.ResponseHeadersRead
};
```

Now, when you call methods such as `ReadString` or `ReadJson`, the response body will be streamed from the socket as it arrives.

The library will also take care of **respecting the timeout you specified in the request**, calculating how much time is left to read the response.

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Get,
    HttpCompletionOption = HttpCompletionOption.ResponseHeadersRead,
    Timeout = TimeSpan.FromSeconds(10)
};

// This call returns immediately after reading the response headers
IHttpResponse response = await request.SendAsync();

Console.WriteLine($"Reading the headers took {response.ElapsedMilliseconds} ms");

// This call will proceed with reading the HTTP response body from the socket
// and will throw HttpRequestTimeoutException if the response body is not
// fully read within 10 total seconds
string body = await response.ReadString();

Console.WriteLine($"Reading the headers+body took {response.ElapsedMilliseconds} ms in total");
```

The exception is if you use the `ReadStream` method: in that case PlainHttp cannot enforce a timeout when reading from that stream outside the library.

You also must take care of disposing the response manually when using `ReadStream`:

```csharp
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Get,
    HttpCompletionOption = HttpCompletionOption.ResponseHeadersRead,
    Timeout = TimeSpan.FromSeconds(10)
};

// You MUST dispose the response manually
// when using HttpCompletionOption.ResponseHeadersRead and ReadStream()
using IHttpResponse response = await request.SendAsync();

Stream stream = await response.ReadStream();
// The timeout is not enforced if you read from `stream` here
```

In all other cases (any other `Read*` method), responses are **always disposed automatically** after reading the response body.

A note on XML deserialization: the `ReadXml` method uses `XmlSerializer`, which is not asynchronous. Therefore, the response body is unfortunately always fully read in memory (asynchronously) before deserializing it, no matter the `HttpCompletionOption` setting.

### Downloading files

You can use the `HttpCompletionOption.ResponseHeadersRead` option to efficiently download files to disk:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Get,
    HttpCompletionOption = HttpCompletionOption.ResponseHeadersRead
};

IHttpResponse response = await request.SendAsync();

await response.DownloadFileAsync("video.mp4");
```

### Proxies

You can set a **custom proxy per request**:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Proxy = new Uri("http://example.org:3128")
};
```

**Proxy credentials** are supported and are automatically parsed from the URI:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Proxy = new Uri("http://user:pass@example.com:3128")
};
```

Note that due to the implementation of proxies in .NET, proxy credentials are only sent from the second request onwards and only if the proxy responded with *407 Proxy Authentication Required*. See [this issue](https://github.com/dotnet/runtime/issues/66244) for more details.

### URL building

This library includes the `Flurl` URL builder as a dependency. Some `Flurl`-provided utilities are used internally but you can also use it to [build URLs](https://flurl.dev/docs/fluent-url/) in an easier way (thanks Todd Menier!):

```c#
string url = "http://random.org"
    .SetQueryParam("locale", "it")
    .SetQueryParam("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
```

### Testing mode

**Unit testing HTTP requests** is easy with PlainHttp. You can enqueue HTTP responses that will be dequeued in sequence.

This mechanism is "async safe": the `TestingMode` property is static but wrapped in to an `AsyncLocal` instance, so that you can run your tests in parallel.

```c#
// Run this once
TestingMode http = new TestingMode();
HttpRequest.SetTestingMode(http);

// Then enqueue HTTP responses
HttpResponseMessage msg = new HttpResponseMessage()
{
    StatusCode = (HttpStatusCode)200,
    Content = new StringContent("oh hello")
};

http.RequestsQueue.Enqueue(msg);

// Then send your requests normally, in the same async context
```

### Custom serialization

You can implement your own custom serializer by implementing the `IPayload` interface.

For example, here's how you can use `Newtonsoft.Json` instead of `System.Text.Json`:

```c#
public class NewtonsoftJsonPayload : IPayload
{
    private readonly object payload;
    private readonly JsonSerializerSettings? settings;
    
    public NewtonsoftJsonPayload(object payload)
    {
        this.payload = payload;
    }

    public NewtonsoftJsonPayload(object payload, JsonSerializerSettings settings) : this(payload)
    {
        this.settings = settings;
    }

    public HttpContent Serialize()
    {
        return new StringContent(
            content: JsonConvert.SerializeObject(payload, settings),
            encoding: Encoding.UTF8,
            mediaType: "application/json"
        );
    }
}
```

Then use it like this:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    Payload = new NewtonsoftJsonPayload(new
    {
        something = "hello"
    })
};
```

### Customizing `HttpClient` defaults

You can customize how `HttpClient`s and the underlying `SocketsHttpHandler` are created by changing the static `HttpClientFactory` property.

The default factory provides some level of customization, which you can pass to the constructor. For example:

```c#
HttpRequest.HttpClientFactory = new HttpClientFactory(new HttpClientFactory.HttpHandlerOptions
{
    IgnoreCertificateValidationErrors = true
});
```

These options will apply to both proxied and non-proxied `HttpClient`s. You can however choose different settings for proxied and non-proxied clients:

```c#
HttpRequest.HttpClientFactory = new HttpClientFactory(
    // Normal requests
    new HttpClientFactory.HttpHandlerOptions
    {
        IgnoreCertificateValidationErrors = true
    },
    // Proxied requests
    new HttpClientFactory.HttpHandlerOptions
    {
        IgnoreCertificateValidationErrors = false
    }
);
```

These are all the available options with their defaults:

```c#
public record HttpHandlerOptions
{
    public TimeSpan PooledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan PooledConnectionIdleTimeout { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan ConnectTimeout { get; init; } = Timeout.InfiniteTimeSpan;
    public DecompressionMethods AutomaticDecompression { get; init; } = DecompressionMethods.All;
    public SslProtocols EnabledSslProtocols { get; init; } = SslProtocols.None;
    public bool IgnoreCertificateValidationErrors { get; init; }
}
```

The meanings of these options (which usually map to `SocketsHttpHandler` properties) are the following:

- `PooledConnectionLifetime`: the maximum lifetime of a connection in the pool.
- `PooledConnectionIdleTimeout`: the maximum idle time of a connection in the pool. If a connection is idle for more than this time, it will be closed.
- `ConnectTimeout`: the timeout for establishing a connection to the server.
- `AutomaticDecompression`: the decompression methods to use for the response body. By default, all methods (gzip, DEFLATE and Brotli) are enabled.
- `EnabledSslProtocols`: the SSL/TLS protocols to use. By default, the system default is used.
- `IgnoreCertificateValidationErrors`: whether to ignore certificate validation errors.

Note that when applied to proxied clients these options will apply to the connection to the proxy server itself. 
