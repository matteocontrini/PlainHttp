using System.Text;
using Flurl.Util;

namespace PlainHttp.Payloads;

public class FormUrlEncodedPayload : IPayload
{
    private readonly object payload;

    public FormUrlEncodedPayload(object payload)
    {
        this.payload = payload;
    }
    
    public HttpContent Serialize()
    {
        // Already serialized
        if (this.payload is string stringPayload)
        {
            return new StringContent(
                content: stringPayload,
                encoding: Encoding.UTF8,
                mediaType: "application/x-www-form-urlencoded"
            );
        }
        else
        {
            var qp = new Flurl.QueryParamCollection();

            foreach ((string key, object value) in this.payload.ToKeyValuePairs())
            {
                qp.AddOrReplace(key, value, false, Flurl.NullValueHandling.Ignore);
            }

            string serialized = qp.ToString(true);

            return new StringContent(
                content: serialized,
                encoding: Encoding.UTF8,
                mediaType: "application/x-www-form-urlencoded"
            );
        }
    }
}
