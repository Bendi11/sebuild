
using Microsoft.CodeAnalysis;

namespace SeBuild;

public sealed class ScriptCommon {
    public Solution Solution;
    public ProjectId ProjectId;

    public Project Project {
        get => Solution.GetProject(ProjectId)!;
    }

    public IEnumerable<DocumentId> Documents {
        get => Project.DocumentIds;
    }

    public IEnumerable<Document> DocumentsIter {
        get => Project.Documents;
    }
    
    /// <summary>
    /// Create new global compilation context for the given <paramref name="project"/>, collecting
    /// all document IDs required to compile the project.
    /// </summary>
    public ScriptCommon(Solution sln, ProjectId project) {
        Solution = sln;
        ProjectId = project;
    }
}
