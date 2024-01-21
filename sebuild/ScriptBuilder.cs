using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Build.Construction;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using SeBuild.Pass.DeadCodeRemover;

namespace SeBuild;

using MSBuildProject = Microsoft.Build.Evaluation.Project;
using MSBuildProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;

public class ScriptBuilder: IDisposable {
    ScriptCommon Common;
    readonly MSBuildWorkspace workspace;
    string scriptDir;
    public string GameScriptDir {
        get => scriptDir;
    }

    public ulong InitialChars = 0;

    bool _workspaceFailed = false;

    static ScriptBuilder() { MSBuildLocator.RegisterDefaults(); }
    
    /// <summary>Create a new <c>ScriptWorkspaceContext</c></summary>
    static async public Task<ScriptBuilder> Create(BuildArgs args) {
        var me = new ScriptBuilder();
        var project = await me.Init(args.SolutionPath, args.Project);

        me.Common = new ScriptCommon(project.Solution, project.Id, args);
        return me;
    }

    void IDisposable.Dispose() {
        workspace.Dispose();
    }

    /// <summary>Build the given <c>Project</c> and return a list of declaration <c>CSharpSyntaxNode</c>s</summary>
    async public Task<IEnumerable<CSharpSyntaxNode>> BuildProject() {
        if(_workspaceFailed) {
            return new List<CSharpSyntaxNode>();
        }

        IEnumerable<Diagnostic>? diags = null;

        //Collect diagnostics before renaming identifiers
        if(Common.Args.RequiresAnalysis) {
            using(var prog = new PassProgress("Analyzing project", PassProgress.Mode.NoProgress)) {
                prog.Report(0);
                diags = (await Common.Project.GetCompilationAsync())!
                    .GetDiagnostics()
                    .Where(d => d.Severity >= DiagnosticSeverity.Warning);
            }
        }

        if(Common.Args.RemoveDead) {
            using(var prog = new PassProgress("Eliminating Dead Code")) {
                var DeadCodePass = new DeadCodeRemover(Common, prog);
                await DeadCodePass.Execute();
            }
        }

        if(Common.Args.Rename) {
            using(var prog = new PassProgress("Renaming Symbols")) {
                var RenamePass = new SeBuild.Pass.Rename.Renamer(Common, prog);
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

    
    
    #pragma warning disable 8618 
    private ScriptBuilder() {
        workspace = MSBuildWorkspace.Create();
    }
    
    /// Find a path to a file of the given extension, using the given path hint
    private string? FindPath(string path, string? extension = "") {
        string dirPath = path;
        bool dir = true;

        try {
            var fa = File.GetAttributes(path);
            dir = fa.HasFlag(FileAttributes.Directory);
        } catch(FileNotFoundException) {
            dirPath = "./";
            dir = true;
        }

        if(dir) {
            var withExtension =
                from file in Directory.GetFiles(dirPath)
                where Path.GetExtension(file).ToUpper().Equals(extension)
                select file;

            if(withExtension.Count() == 1) {
                return withExtension.First();
            } else {
                foreach(var file in withExtension) {
                    if(Path.GetFileNameWithoutExtension(file)
                            .Equals(Path.GetFileNameWithoutExtension(path))
                    ) {
                        return file;
                    }
                }
            }
        } else {
            return path;
        }

        return null;
    }


    async private Task<Project> Init(string slnPath, string projectPath) {
        workspace.WorkspaceFailed += (_, wsDiag) => {
            if(wsDiag.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning) { return; }
            _workspaceFailed = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(wsDiag.Diagnostic.Message);
        };

        string slnFile = slnPath, projectFile = projectPath;
        try {
            slnFile = FindPath(slnPath, ".SLN") ??
                throw new Exception($"Failed to find solution file using path {slnPath}");
            projectFile = FindPath(projectPath, ".CSPROJ") ??
                throw new Exception($"Failed to find project file with path {projectPath}");
        } catch(Exception e) {
            Console.WriteLine(e.Message);
        }

        using(var progress = new PassProgress($"Read solution {slnFile}")) {
            var project = await workspace.OpenProjectAsync(
                projectFile,
                new Progress<ProjectLoadProgress>(
                    loadProgress => {
                        //progress.Message = $"{loadProgress.Operation} {loadProgress.FilePath}";
                        progress.Report(1);
                    }
                )
            );

            // Now we use the MSBuild apis to load and evaluate our project file
            using var xmlReader = XmlReader.Create(
                File.OpenRead(projectFile)
            );

            try {
                ProjectRootElement root = ProjectRootElement.Create(
                    xmlReader,
                    new MSBuildProjectCollection(),
                    preserveFormatting: true
                );
                MSBuildProject msbuildProject = new MSBuildProject(root);
                scriptDir = msbuildProject.GetPropertyValue("SpaceEngineersScript");
                if(scriptDir.Length == 0) { throw new Exception("No SpaceEngineersScript property defined in env.csproj"); }

            } catch(Exception e) {
                Console.WriteLine(e.Message);
            }
            
            return project;
        }
    }
}
