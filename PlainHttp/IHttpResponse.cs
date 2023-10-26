namespace PlainHttp;

public interface IHttpResponse
{
    HttpRequest Request { get; }
    HttpResponseMessage Message { get; }
    string? Body { get; }
    bool Succeeded { get; }
    TimeSpan ElapsedTime { get; }

    string? GetSingleHeader(string name);
    Task ReadBody();
    void EnsureSuccessStatusCode();
}
