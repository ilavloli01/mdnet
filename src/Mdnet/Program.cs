using System.CommandLine;
using Mdnet.Cli;

var root = new RootCommand("mdnet: AI-friendly API documentation for .NET libraries (Markdown + Markdoc HTML).")
{
    GenerateCommand.Create(),
    RenderCommand.Create(),
    DownloadCommand.Create(),
    SyncCommand.Create(),
};

return await root.Parse(args).InvokeAsync();
