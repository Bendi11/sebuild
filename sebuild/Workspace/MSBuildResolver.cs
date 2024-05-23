
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using MSBuildProject = Microsoft.Build.Evaluation.Project;

namespace SeBuild;

/// <summary>
/// Loads MSBuild project files and adds their source files to a main project to be processed later.
/// Also resolves NuGet PackageReference tags by downloading their source to a cache directory and 
/// using those source files for compilation
/// </summary>
public sealed class MSBuildResolver: IDisposable {
    /// Script context that contains the project with flat list of files to add items to
    private ScriptCommon _ctx;

    private MSBuildWorkspace _workspace;

    void IDisposable.Dispose() {
        _workspace.Dispose();
    }
    
    /// <summary>
    /// Create a new resolver that will add source files to the given script context
    /// </summary>
    public MSBuildResolver(ScriptCommon ctx, MSBuildWorkspace workspace) {
        _ctx = ctx;
        _workspace = workspace;
    }

    public async Task AddProjectSources(string projectPath) {
        
    }
    
    /// <summary>
    /// Add all sources of the given project to the script context, and download all 
    /// referenced package sources to a cache directory for later analysis
    /// </summary>
    public async Task AddProjectSources(Project project) {
        foreach(var i in project.MetadataReferences) {
            Console.WriteLine(i.Display);
        }
    }
}
