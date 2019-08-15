using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PlainHttp
{
    public interface IHttpRequest
    {
        ContentType ContentType { get; set; }
        string DownloadFileName { get; set; }
        Dictionary<string, string> Headers { get; set; }
        HttpRequestMessage Message { get; }
        HttpMethod Method { get; set; }
        object Payload { get; set; }
        Uri Proxy { get; set; }
        TimeSpan Timeout { get; set; }
        Uri Uri { get; set; }

        Task<HttpResponse> SendAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
