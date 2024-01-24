using System.Text;

namespace PlainHttp.Payloads;

public class PlainTextPayload : IPayload
{
    private readonly string payload;

    public PlainTextPayload(string payload)
    {
        this.payload = payload;
    }
    
    public HttpContent Serialize()
    {
        return new StringContent(
            content: this.payload,
            encoding: Encoding.UTF8,
            mediaType: "text/plain"
        );
    }
}
