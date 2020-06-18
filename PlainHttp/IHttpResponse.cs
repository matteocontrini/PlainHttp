using System.Net.Http;
using System.Threading.Tasks;

namespace PlainHttp
{
    public interface IHttpResponse
    {
        string Body { get; set; }
        HttpResponseMessage Message { get; set; }
        HttpRequest Request { get; set; }
        bool Succeeded { get; }

        string GetSingleHeader(string name);
        Task ReadBody();
    }
}
