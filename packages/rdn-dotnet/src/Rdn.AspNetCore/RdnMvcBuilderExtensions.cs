using Microsoft.Extensions.DependencyInjection;
using Rdn;

namespace Rdn.AspNetCore;

public static class RdnMvcBuilderExtensions
{
    public static IMvcBuilder AddRdnFormatters(this IMvcBuilder builder, RdnSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(mvcOptions =>
        {
            var serializerOptions = options ?? RdnSerializerOptions.Default;
            mvcOptions.InputFormatters.Add(new RdnInputFormatter(serializerOptions));
            mvcOptions.OutputFormatters.Add(new RdnOutputFormatter(serializerOptions));
        });

        return builder;
    }

    public static IMvcBuilder AddRdnFormatters(this IMvcBuilder builder, Action<RdnSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RdnSerializerOptions();
        configure(options);

        return builder.AddRdnFormatters(options);
    }
}
