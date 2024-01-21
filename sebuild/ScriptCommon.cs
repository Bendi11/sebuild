
using Microsoft.CodeAnalysis;

namespace SeBuild;

public sealed class ScriptCommon {
    public Solution Solution;
    public ProjectId ProjectId;
    public List<DocumentId> Documents;
    public BuildArgs Args;

    public Project Project {
        get => Solution.GetProject(ProjectId)!;
    }

    public IEnumerable<Document> DocumentsIter {
        get {
            foreach(var id in Documents) {
                yield return Solution.GetDocument(id)!;
            }
        }
    }
    
    /// <summary>
    /// Create new global compilation context for the given <paramref name="project"/>, collecting
    /// all document IDs required to compile the project.
    /// </summary>
    public ScriptCommon(Solution sln, ProjectId project, BuildArgs args) {
        Solution = sln;
        ProjectId = project;
        Args = args;
        Documents = new List<DocumentId>();
        GetDocuments(ProjectId, new HashSet<ProjectId>());
    }
    
    /// <summary>
    /// Collect all the source documents that must be processed for the project
    /// </summary>
    private void GetDocuments(ProjectId id, HashSet<ProjectId> loadedProjects) {
        if(loadedProjects.Contains(id)) { return; }
        loadedProjects.Add(id);

        foreach(var doc in Solution.GetProject(id)!.Documents) {
            Documents.Add(doc.Id);
        }

        foreach(var dep in Solution.GetProject(id)!.ProjectReferences) {
            GetDocuments(dep.ProjectId, loadedProjects);
        }
    }
}
