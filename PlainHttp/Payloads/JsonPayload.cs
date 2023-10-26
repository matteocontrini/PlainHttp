using System.Text;
using System.Text.Json;

namespace PlainHttp.Payloads;

public class JsonPayload : IPayload
{
    private readonly object payload;
    private readonly JsonSerializerOptions? options;

    public JsonPayload(object payload)
    {
        this.payload = payload;
    }

    public JsonPayload(object payload, JsonSerializerOptions options) : this(payload)
    {
        this.options = options;
    }

    public HttpContent Serialize()
    {
        string serialized;

        // Already serialized
        if (this.payload is string stringPayload)
        {
            serialized = stringPayload;
        }
        else
        {
            serialized = JsonSerializer.Serialize(this.payload, this.options);
        }

        return new StringContent(
            content: serialized,
            encoding: Encoding.UTF8,
            mediaType: "application/json"
        );
    }
}
