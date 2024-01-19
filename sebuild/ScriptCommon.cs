
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

    public ScriptCommon(Solution sln, ProjectId project, List<DocumentId> docs, BuildArgs args) {
        Solution = sln;
        ProjectId = project;
        Documents = docs;
        Args = args;
    }
}
