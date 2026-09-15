namespace Mdnet;

/// <summary>Console diagnostics on stderr, so stdout stays clean.</summary>
public sealed class Log(bool verbose = false, bool quiet = false)
{
    public static readonly Log Silent = new(quiet: true);

    public void Info(string message)
    {
        if (!quiet)
        {
            Console.Error.WriteLine(message);
        }
    }

    public void Verbose(string message)
    {
        if (verbose && !quiet)
        {
            Console.Error.WriteLine("  " + message);
        }
    }

    public void Warn(string message) => Write(ConsoleColor.Yellow, "warning: " + message);

    public void Error(string message) => Write(ConsoleColor.Red, "error: " + message);

    private static void Write(ConsoleColor color, string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}
