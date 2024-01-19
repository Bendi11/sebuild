using CommandLine;

namespace SeBuild;

[Verb("build", HelpText="build a script project into an output file")]
public class BuildArgs {
    [Option('s', "sln", HelpText = "Path to a solution file or a directory containing one", Default = "./")]
    public string SolutionPath { get; set; } = "./";

    [Value(0, Required=true, MetaName="script", HelpText="Name of the project in solution")]
    public string Project { get; set; } = "";

    [Option('o', "output", Required=false, HelpText="Path to write a compressed output file")]
    public string? Output { get; set; }

    [Option('m', "minify", Required=false, HelpText = "Minify the produced output")]
    public bool Minify { get; set; }

    [Option('r', "rename", Required = false, HelpText = "Rename symbols to reduce output size further")]
    public bool Rename { get; set; }

    [Option(
        'd',
        "remove-dead",
        Required = false,
        HelpText = "Remove dead code not referenced by the Program class"
    )]
    public bool RemoveDead { get; set; }
    
    [Option(
        'D',
        "diagnostics",
        Required = false,
        HelpText = "Print diagnostics for project even if no code size reductions are in place (adds around 2s to build)"
    )]
    public bool Diagnostics { get; set; }
    
    /// Check if code analysis is required for this project compilation
    public bool RequiresAnalysis {
        get => Rename || RemoveDead || Diagnostics;
    }
}
