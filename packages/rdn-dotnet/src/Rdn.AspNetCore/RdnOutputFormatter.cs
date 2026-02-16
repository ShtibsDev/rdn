using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Rdn;

namespace Rdn.AspNetCore;

public class RdnOutputFormatter : TextOutputFormatter
{
    private readonly RdnSerializerOptions _options;

    public RdnOutputFormatter() : this(RdnSerializerOptions.Default) { }

    public RdnOutputFormatter(RdnSerializerOptions options)
    {
        _options = options;

        SupportedMediaTypes.Add("application/rdn");

        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selectedEncoding);

        var httpContext = context.HttpContext;
        var outputStream = httpContext.Response.Body;

        Stream writeStream = outputStream;
        if (!selectedEncoding.Equals(Encoding.UTF8))
        {
            writeStream = Encoding.CreateTranscodingStream(outputStream, selectedEncoding, Encoding.UTF8, leaveOpen: true);
        }

        try
        {
            var objectType = context.Object?.GetType() ?? context.ObjectType ?? typeof(object);
            await RdnSerializer.SerializeAsync(writeStream, context.Object, objectType, _options, httpContext.RequestAborted);
            await writeStream.FlushAsync(httpContext.RequestAborted);
        }
        finally
        {
            if (writeStream != outputStream)
            {
                await writeStream.DisposeAsync();
            }
        }
    }
}
