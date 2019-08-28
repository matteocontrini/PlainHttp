# PlainHttp ![NuGet](https://img.shields.io/nuget/v/PlainHttp?color=green) ![License](https://img.shields.io/github/license/matteocontrini/PlainHttp)

An easy HTTP client for .NET Core 2.2 with support for serialization, proxies, testing and more

## Features

- Wraps `HttpClient` and provides a cleaner and easier interface
- Supports any HTTP method
- Per-request timeout with a custom `HttpRequestTimeoutException`
- Per-request proxy with transparent pooling
- Automatic serialization of objects to `JSON`/`XML`/URL encoded
- (no deserialization support)
- Download a file to disk
- Set a response encoding
- Automatically enabled decompression of GZIP and DEFLATE  responses
- Allows to mock requests for unit testing
- Heavily used in production by [@botfactoryit](https://github.com/botfactoryit/) to send 1 million requests *per day*

## Why .NET Core 2.2

This library targets .NET Core 2.2 because it requires the `PooledConnectionLifetime` property on `HttpMessageHandler`, introduced in .NET Core 2.2. This makes sure that reusing the same `HttpClient` for a long time doesn't have [unintended consequences](https://github.com/dotnet/corefx/issues/11224) affecting DNS resolution. This library in fact keeps a pool of `HttpClient` instances that are never disposed.

In particular, the library keeps:

- One `HttpClient` per request host
- One `HttpClient` per proxy

There is currently no mechanism that disposes `HttpClient` instances that are unused, so if you use a lot of random proxies or many different hostnames, you might get into trouble. This can be easily improved by creating a custom [`IHttpClientFactory`](https://github.com/matteocontrini/PlainHttp/blob/ba9e51629629fb8fafbf3c8ac7335e5c09c15cfc/PlainHttp/HttpClientFactory.cs), and then passing the factory to each request through the `HttpClientFactory` property.

## Usage

Basic `GET` request:

```c#
string url = "http://random.org";
IHttpRequest request = new HttpRequest(url);
IHttpResponse response = await request.SendAsync();
string body = response.Body;
```

Also with `Uri`:

```c#
Uri uri = new Uri("http://random.org");
IHttpRequest request = new HttpRequest(uri);
```

Checking if the HTTP status code is in the `2xx` range:

```c#
IHttpResponse response = await request.SendAsync();

if (!response.Succeeded)
{
    Console.WriteLine($"Response status code is {response.Message.StatusCode");
}
else
{
    Console.WriteLine($"Response length is {response.Body.Length}");
}
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

Setting custom headers:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Headers = new Dictionary<string, string>
    {
        // note that no user agent is set by default
        { "User-Agent", "PlainHttp/1.0" }
    }
};
```

Custom response decoding:

```c#
IHttpRequest request = new HttpRequest(url)
{
    ResponseEncoding = Encoding.GetEncoding("ISO-8859-1")
};
```

Custom timeout (note that by default there is no timeout):

```c#
IHttpRequest request = new HttpRequest(url)
{
    Timeout = TimeSpan.FromSeconds(10)
};
```

`POST` request with URL-encoded payload:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    ContentType = ContentType.UrlEncoded,
    Payload = new
    {
        something = "hello",
        buuu = true
    }
};
```

`POST` request with `JSON` payload:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    ContentType = ContentType.Json,
    // you can pass any object, it will be passed to JSON.NET
    Payload = new
    {
        something = "web"
    }
};
```

`POST` request with `XML` payload:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    ContentType = ContentType.Json,
    // the object will be passed to System.Xml.Serialization.XmlSerializer
    Payload = new
    {
        something = "web"
    }
};
```

You can also choose a content type and then pass an already serialized string:

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Post,
    ContentType = ContentType.Json,
    Payload = "{ \"key\": true }"
};
```

There is currently no response deserialization. The body will be automatically converted to string with the encoding specified by the `ResponseEncoding` property.

If you specify the `DownloadFileName` property, the response will be saved to file and the response `Body` property will be `null`.

```c#
IHttpRequest request = new HttpRequest(url)
{
    Method = HttpMethod.Get,
    DownloadFileName = "page.html"
};
```

Set a custom proxy per request (make sure you're aware of the pitfalls mentioned above):

```c#
IHttpRequest request = new HttpRequest(url)
{
    Proxy = new Uri("http://yyyy.org:3128")
};
```

This library wraps the `Flurl` URL builder that provides some utilities that are used internally. You can anyway use `Flurl` to build URLs in an easier way:

```c#
string url = "http://random.org"
    .SetQueryParam("locale", "it")
    .SetQueryParam("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
```

Unit testing HTTP requests is easy with PlainHttp. You can enqueue HTTP responses that will be dequeued in sequence. This mechanism is "async safe": the `TestingMode` property is static but wrapped in to an `AsyncLocal` instance, so that you can run your tests in parallel.

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
```
