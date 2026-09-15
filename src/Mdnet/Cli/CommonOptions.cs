namespace Mdnet.Cli;

using System.CommandLine;

internal static class CommonOptions
{
    public static Log CreateLog(ParseResult result) => new(result.GetValue<bool>("--verbose"), result.GetValue<bool>("--quiet"));

    public static Command WithLogging(this Command command)
    {
        command.Options.Add(new Option<bool>("--verbose", "-v") { Description = "Show detailed progress." });
        command.Options.Add(new Option<bool>("--quiet", "-q") { Description = "Only show warnings and errors." });
        return command;
    }

    /// <summary>Runs a command body, turning expected failures into an error message and exit code 1.</summary>
    public static async Task<int> RunAsync(Log log, Func<Task<int>> body)
    {
        try
        {
            return await body();
        }
        catch (Exception e)
            when (e
                    is FileNotFoundException
                        or DirectoryNotFoundException
                        or ArgumentException
                        or InvalidDataException
                        or InvalidOperationException
                        or HttpRequestException
                        or System.Text.Json.JsonException
            )
        {
            log.Error(e.Message);
            return 1;
        }
    }
}
