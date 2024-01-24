namespace PlainHttp.Payloads;

public interface IPayload
{
    HttpContent Serialize();
}
