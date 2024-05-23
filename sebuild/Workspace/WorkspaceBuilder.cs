
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace SeBuild;

/// <summary>
/// Class responsible for creating a Roslyn workspace containing all documents required to compile the requested project.
/// Supports reading MSBuild .csproj files and downloading `PackageReference`d package sources from those project files,
/// and resolving local ProjectReference items. 
/// Also supports raw file lists or single file builds for simple scripts.
/// </summary>
public sealed class WorkspaceBuilder {
    /// Project name used when only a list of script files is given as sources
    private static readonly string DefaultProjectName = "FilesProject";
    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(
        documentationMode: DocumentationMode.None
    );
    
    private Paths _paths;

    public WorkspaceBuilder(Paths paths) {
        _paths = paths;
    }

    private ScriptCommon NewContext(string projectName) {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(projectName, LanguageNames.CSharp);
        return new ScriptCommon(workspace.CurrentSolution, project.Id);
    }
    
    /// <summary>
    /// Create a new workspace from the given project,
    /// loading a Visual Studio solution from the given <paramref name="slnFile">solution file</paramref> if it is provided.
    /// Otherwise, attempts to find a matching .csproj file with the file <paramref name="projectName"/>name</paramref>
    /// </summary>
    public async Task<ScriptCommon> CreateFromMSBuild(string? projectName) {
        MSBuildLocator.RegisterDefaults();

        bool workspaceError = false;
        var msBuildWorkspace = MSBuildWorkspace.Create();
        msBuildWorkspace.WorkspaceFailed += (_, wsDiag) => {
            if(wsDiag.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning) { return; }
            workspaceError = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(wsDiag.Diagnostic.Message);
        };
        
        string projectPath = FindPath(projectName ?? "./", ".CSPROJ") ??
            throw new Exception($"Failed to find a .csproj file for the given project");
        
        
        var ctx = NewContext(Path.GetFileNameWithoutExtension(projectPath));
        MSBuildResolver resolver = new MSBuildResolver(ctx, msBuildWorkspace);
        await resolver.AddProjectSources(projectPath);

        if(workspaceError) {
            throw new Exception("MSBuild fatal error");
        }
        return ctx;
    }
    
    /// <summary>
    /// Create a new workspace using only the provided <paramref name="paths">file paths</paramref>.
    /// Automatically adds metadata references for the space engineers scripting DLLs for error checking.
    /// </summary>
    public async Task<ScriptCommon> CreateFromFiles(params string[] paths) {
        var ctx = NewContext(DefaultProjectName);
        foreach(var path in paths) {
            string fileContent = await File.ReadAllTextAsync(path);
            var tree = CSharpSyntaxTree.ParseText(fileContent, ParseOptions, path);
            ctx.Project.AddDocument(path, await tree.GetRootAsync(), filePath: path);
        }

        ctx.Project.AddMetadataReferences(
            SpaceEngineersScriptAssemblies.Select((name, _) =>
                MetadataReference.CreateFromFile(Path.Combine(_paths.SEBinPath, $"{name}.dll"))
            )
        );
        
        return ctx;
    }

    /// <summary>
    /// Find a path to a file of the given <paramref name="extension"/>,
    /// </summary>
    /// <param name="path">Path without file extension of the file to locate</param>
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

    private static readonly string[] SpaceEngineersScriptAssemblies = {
        "System.Collections.Immutable",
        "Sandbox.Common",
        "Sandbox.Game",
        "Sandbox.Graphics",
        "SpaceEngineers.Game",
        "SpaceEngineers.ObjectBuilders",
        "VRage",
        "VRage.Audio",
        "VRage.Game",
        "VRage.Input",
        "VRage.Library",
        "VRage.Math",
        "VRage.Render",
        "VRage.Render11",
        "VRage.Scripting",
    };
}
