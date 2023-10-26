using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace PlainHttp.Payloads;

public class XmlPayload : IPayload
{
    private readonly object payload;
    private readonly XmlWriterSettings? settings;

    public XmlPayload(object payload)
    {
        this.payload = payload;
    }

    public XmlPayload(object payload, XmlWriterSettings settings) : this(payload)
    {
        this.settings = settings;
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
            XmlSerializer serializer = new XmlSerializer(this.payload.GetType());
            StringBuilder result = new StringBuilder();

            using (var writer = XmlWriter.Create(result, this.settings))
            {
                serializer.Serialize(writer, this.payload);
            }

            serialized = result.ToString();
        }

        return new StringContent(
            content: serialized,
            encoding: Encoding.UTF8,
            mediaType: "text/xml"
        );
    }
}
