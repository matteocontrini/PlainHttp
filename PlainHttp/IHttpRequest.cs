using System;
using System.Collections.Generic;
using System.Net;
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
        IWebProxy Proxy { get; set; }
        TimeSpan Timeout { get; set; }
        Uri Uri { get; set; }
        HttpCompletionOption HttpCompletionOption { get; set; }
        bool ReadBody { get; set; }
        Version HttpVersion { get; set; }

        Task<IHttpResponse> SendAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
