using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SeBuild.Pass.DeadCodeRemover;
using SeBuild.Pass.Rename;

namespace SeBuild;



/// <summary>
/// Container for all compilation passes that manages the compilation sequence including renaming and deleting, and reducing a project
/// to a flat list of definition syntax nodes that can be written to a final file
/// </summary>
public class ScriptBuilder {
    /// <summary>
    /// State that is shared between compilation passses
    /// </summary>
    public ScriptCommon Common {
        get;
        private set;
    }

    public ScriptBuilder(ScriptCommon ctx) {
        Common = ctx;
    }

    /// <summary>Build the given <c>Project</c> and return a list of declaration <c>CSharpSyntaxNode</c>s</summary>
    async public Task<IEnumerable<CSharpSyntaxNode>> BuildProject(BuildArgs args) {
        IEnumerable<Diagnostic>? diags = null;

        //Collect diagnostics before renaming identifiers
        if(args.RequiresAnalysis) {
            using(var prog = new PassProgress("Analyzing project", PassProgress.Mode.NoProgress)) {
                prog.Report(0);
                diags = (await Common.Project.GetCompilationAsync())!
                    .GetDiagnostics()
                    .Where(d => d.Severity >= DiagnosticSeverity.Warning);
            }
        }

        if(args.RemoveDead) {
            using(var prog = new PassProgress("Eliminating Dead Code")) {
                var DeadCodePass = new DeadCodeRemover(Common, prog);
                await DeadCodePass.Execute();
            }
        }

        if(args.Rename) {
            using(var prog = new PassProgress("Renaming Symbols")) {
                var RenamePass = new Renamer(Common, prog);
                await RenamePass.Execute();
            }
        }
        
        if(diags is not null) {
            foreach(var diag in diags) {
                Console.ForegroundColor = diag.Severity switch {
                    DiagnosticSeverity.Error => ConsoleColor.Red,
                    DiagnosticSeverity.Warning => ConsoleColor.Yellow,
                    DiagnosticSeverity.Info => ConsoleColor.White,
                    DiagnosticSeverity.Hidden => ConsoleColor.Gray,
                    var _ => ConsoleColor.White,
                };
                Console.WriteLine(diag);
            }

            Console.ResetColor();
        }
        
        using(var prog = new PassProgress("Flattening Declarations")) {
            return await Preprocessor.Build(Common, prog);
        }
    }
}
