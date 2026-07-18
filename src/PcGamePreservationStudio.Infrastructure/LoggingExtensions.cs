using Microsoft.Extensions.Logging;

namespace PcGamePreservationStudio.Infrastructure;

public static class LoggingExtensions
{
    public static ILoggingBuilder AddPcGamePreservationStudioLogging(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
        builder.AddDebug();
        builder.SetMinimumLevel(LogLevel.Information);
        return builder;
    }
}
