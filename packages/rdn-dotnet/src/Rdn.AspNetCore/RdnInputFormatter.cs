using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Rdn;

namespace Rdn.AspNetCore;

public class RdnInputFormatter : TextInputFormatter
{
    private readonly RdnSerializerOptions _options;

    public RdnInputFormatter() : this(RdnSerializerOptions.Default) { }

    public RdnInputFormatter(RdnSerializerOptions options)
    {
        _options = options;

        SupportedMediaTypes.Add("application/rdn");

        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(encoding);

        var httpContext = context.HttpContext;
        var inputStream = httpContext.Request.Body;

        Stream readStream = inputStream;
        if (!encoding.Equals(Encoding.UTF8))
        {
            readStream = Encoding.CreateTranscodingStream(inputStream, encoding, Encoding.UTF8, leaveOpen: true);
        }

        try
        {
            var result = await RdnSerializer.DeserializeAsync(readStream, context.ModelType, _options, httpContext.RequestAborted);
            return await InputFormatterResult.SuccessAsync(result);
        }
        catch (RdnException)
        {
            return await InputFormatterResult.FailureAsync();
        }
        finally
        {
            if (readStream != inputStream)
            {
                await readStream.DisposeAsync();
            }
        }
    }
}
