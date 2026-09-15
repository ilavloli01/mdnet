using System.CommandLine;
using Mdnet.Rendering;

namespace Mdnet.Cli;

internal static class RenderCommand
{
    public static Option<string> RuntimeOption()
    {
        var option = new Option<string>("--runtime")
        {
            Description = "JavaScript runtime used to render HTML: auto, bun or node.",
            DefaultValueFactory = _ => "auto",
        };
        option.AcceptOnlyFromAmong("auto", "bun", "node");
        return option;
    }

    public static Command Create()
    {
        var input = new Argument<string>("input")
        {
            Description = "Markdown docs root (generated or downloaded), e.g. docs/md or .mdnet/docs.",
        };
        var output = new Option<string>("--output", "-o") { Description = "HTML output directory.", Required = true };
        var runtime = RuntimeOption();

        var command = new Command("render", "Render a Markdown docs folder into a static HTML site.") { input, output, runtime }.WithLogging();
        command.SetAction(
            (result, cancellationToken) =>
            {
                var log = CommonOptions.CreateLog(result);
                return CommonOptions.RunAsync(
                    log,
                    () =>
                        MarkdocRenderer.RenderAsync(
                            Path.GetFullPath(result.GetValue(input)!),
                            Path.GetFullPath(result.GetValue(output)!),
                            result.GetValue(runtime)!,
                            log,
                            cancellationToken
                        )
                );
            }
        );
        return command;
    }
}
