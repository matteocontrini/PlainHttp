namespace PlainHttp;

class StreamHelper
{
    public static async Task<MemoryStream> ConvertToMemoryStream(Stream stream)
    {
        if (stream is MemoryStream memoryStream)
        {
            return memoryStream;
        }
        else
        {
            memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}
