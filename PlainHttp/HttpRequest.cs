using Flurl.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace PlainHttp
{
    /// <summary>
    /// A wrapper for making HTTP requests simpler.
    /// Handles serialization, proxy, timeout, file download
    /// </summary>
    public class HttpRequest : IHttpRequest
    {
        public HttpMethod Method { get; set; } = HttpMethod.Get;

        public Uri Uri { get; set; }

        public static IHttpClientFactory HttpClientFactory { get; set; }
            = new HttpClientFactory();

        public HttpRequestMessage Message { get; protected set; }

        public TimeSpan Timeout { get; set; }
            = TimeSpan.Zero;

        public Dictionary<string, string> Headers { get; set; }
            = new Dictionary<string, string>();

        public IWebProxy Proxy { get; set; }

        public object Payload { get; set; }

        public PayloadSerializationType PayloadSerializationType { get; set; }

        public string DownloadFileName { get; set; }

        public Encoding ResponseEncoding { get; set; }

        public HttpCompletionOption HttpCompletionOption { get; set; } = HttpCompletionOption.ResponseContentRead;

        public bool ReadBody { get; set; } = true;

        public Version HttpVersion { get; set; }

        private static AsyncLocal<TestingMode> testingMode
            = new AsyncLocal<TestingMode>();

        public HttpRequest()
        {
        }

        public HttpRequest(Uri uri)
        {
            this.Uri = uri;
        }

        public HttpRequest(string url)
        {
            this.Uri = new Uri(url);
        }

        public async Task<IHttpResponse> SendAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (testingMode.Value != null)
            {
                return await MockedResponse().ConfigureAwait(false);
            }

            HttpClient client;

            if (this.Proxy != null)
            {
                client = HttpClientFactory.GetProxiedClient(this.Proxy);
            }
            else
            {
                client = HttpClientFactory.GetClient(this.Uri);
            }

            HttpRequestMessage requestMessage = new HttpRequestMessage
            {
                Method = this.Method,
                RequestUri = this.Uri
            };

            // Add the headers to the request
            foreach (string headerName in this.Headers.Keys)
            {
                requestMessage.Headers.TryAddWithoutValidation(headerName, this.Headers[headerName]);
            }

            // Save the HttpRequestMessage
            this.Message = requestMessage;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Enable timeout, if set
            if (this.Timeout != TimeSpan.Zero)
            {
                cts.CancelAfter(this.Timeout);
            }

            // Set the HTTP protocol version to be used
            if (this.HttpVersion != null)
            {
                requestMessage.Version = HttpVersion;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                HttpResponseMessage responseMessage;

                // Serialize the payload
                if (this.Payload != null)
                {
                    if (this.PayloadSerializationType == PayloadSerializationType.Json)
                    {
                        SerializeToJson(requestMessage);
                    }
                    else if (this.PayloadSerializationType == PayloadSerializationType.Xml)
                    {
                        SerializeToXml(requestMessage);
                    }
                    else if (this.PayloadSerializationType == PayloadSerializationType.UrlEncoded)
                    {
                        SerializeToUrlEncoded(requestMessage);
                    }
                    // Raw
                    else
                    {
                        requestMessage.Content = new StringContent(this.Payload.ToString());
                    }
                }

                // Send the request
                responseMessage = await client.SendAsync(requestMessage, this.HttpCompletionOption, cts.Token).ConfigureAwait(false);

                // Wrap the content into an HttpResponse instance,
                // also reading the body (string or file), if requested
                IHttpResponse response = await CreateHttpResponse(responseMessage).ConfigureAwait(false);

                response.ElapsedTime = stopwatch.Elapsed;

                return response;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    throw new HttpRequestTimeoutException(this, ex);
                }

                throw new HttpRequestException(this, ex);
            }
        }

        private async Task<IHttpResponse> CreateHttpResponse(HttpResponseMessage responseMessage)
        {
            // No file name given, read the body of the response as a string
            if (this.DownloadFileName == null)
            {
                IHttpResponse response = new HttpResponse(this, responseMessage);

                if (this.ReadBody)
                {
                    await response.ReadBody().ConfigureAwait(false);
                }

                return response;
            }
            // Copy the response to a file
            else
            {
                using (Stream stream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (FileStream fs = new FileStream(this.DownloadFileName, FileMode.Create, FileAccess.Write))
                {
                    await stream.CopyToAsync(fs).ConfigureAwait(false);
                }

                return new HttpResponse(this, responseMessage);
            }
        }

        private async Task<IHttpResponse> MockedResponse()
        {
            // Get the testing mode instance for this async context
            HttpResponseMessage message = testingMode.Value.RequestsQueue.Dequeue();

            return await CreateHttpResponse(message).ConfigureAwait(false);
        }

        private void SerializeToUrlEncoded(HttpRequestMessage requestMessage)
        {
            // Already serialized
            if (this.Payload is string)
            {
                requestMessage.Content = new StringContent(
                    content: this.Payload.ToString(),
                    encoding: Encoding.UTF8,
                    mediaType: "application/x-www-form-urlencoded"
                );
            }
            else
            {
                var qp = new Flurl.QueryParamCollection();

                foreach ((string key, object value) in this.Payload.ToKeyValuePairs())
                {
                    qp.AddOrReplace(key, value, false, Flurl.NullValueHandling.Ignore);
                }

                string serialized = qp.ToString(true);

                requestMessage.Content = new StringContent(
                    content: serialized,
                    encoding: Encoding.UTF8,
                    mediaType: "application/x-www-form-urlencoded"
                );
            }
        }

        private void SerializeToXml(HttpRequestMessage requestMessage)
        {
            string serialized;

            // Already serialized
            if (this.Payload is string)
            {
                serialized = this.Payload.ToString();
            }
            else
            {
                XmlSerializer serializer = new XmlSerializer(this.Payload.GetType());
                StringBuilder result = new StringBuilder();

                using (var writer = XmlWriter.Create(result))
                {
                    serializer.Serialize(writer, this.Payload);
                }

                serialized = result.ToString();
            }

            requestMessage.Content = new StringContent(
                content: serialized,
                encoding: Encoding.UTF8,
                mediaType: "text/xml"
            );
        }

        private void SerializeToJson(HttpRequestMessage requestMessage)
        {
            string serialized;

            // Already serialized
            if (this.Payload is string)
            {
                serialized = this.Payload.ToString();
            }
            else
            {
                serialized = JsonSerializer.Serialize(this.Payload);
            }

            requestMessage.Content = new StringContent(
                content: serialized,
                encoding: Encoding.UTF8,
                mediaType: "application/json"
            );
        }

        public override string ToString()
        {
            return $"{this.Method} {this.Uri}";
        }

        public static void SetTestingMode(TestingMode t)
        {
            testingMode.Value = t;
        }
    }
}
