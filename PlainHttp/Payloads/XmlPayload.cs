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

            using var stringWriter = new Utf8StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, this.settings);

            serializer.Serialize(xmlWriter, this.payload);
            serialized = stringWriter.ToString();
        }

        return new StringContent(
            content: serialized,
            encoding: Encoding.UTF8,
            mediaType: "text/xml"
        );
    }

    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
