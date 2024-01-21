using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Rename;

namespace SeBuild.Pass.Rename;

public class Renamer: CompilationPass {
    //Context shared between several rename passes
    RenamerWalkerContext _ctx; 
    
    //Options to use with Roslyn's provided renaming API
    private static readonly SymbolRenameOptions _opts = new SymbolRenameOptions() {
        RenameOverloads = true,
        RenameFile = false,
        RenameInComments = false,
        RenameInStrings = false,
    };
    
    
    /// <summary>
    /// Create a new rename compilation pass
    /// </summary>
    public Renamer(ScriptCommon ctx, PassProgress prog) : base(ctx, prog) {
        _ctx = new RenamerWalkerContext();
    }

    private delegate RenamerWalkerBase ConstructRenamer(SemanticModel sema, List<Task> tasks);
    
    /// <summary>
    /// Save a list of syntax updates to be applied at a later time using the given renamer
    /// </summary>
    private async Task RenameWith<T>(ConstructRenamer New) where T: RenamerWalkerBase {
        List<Task> tasks = new List<Task>();
        foreach(var docId in Common.Documents) {
            var doc = Common.Solution.GetDocument(docId)!;
            var project = Common.Solution.GetProject(doc.Project.Id)!;
            var comp = (await project.GetCompilationAsync())!;

            var tree = (await doc.GetSyntaxTreeAsync())!;
            var sema = comp.GetSemanticModel(tree)!;
           
            var walker = New(sema, tasks);
            walker.Visit(await tree.GetRootAsync());
        }

        await Task.WhenAll(tasks);
    }

    /// Rename all identifiers in the given project
    async public override Task Execute() {
        await RenameWith<InterfaceRenamerWalker>((sema, tasks) => new InterfaceRenamerWalker(Common.Solution, _ctx, sema, tasks));
        await RenameWith<RenameIdentWalker>((sema, tasks) => new RenameIdentWalker(Common.Solution, _ctx, sema, tasks));


        var modifications = new MultiMap<DocumentId, TextChange>();
        var unique = new Dictionary<(DocumentId, TextSpan), string>();

        foreach(var renamedSymbol in _ctx.Renamed) {
            Tick();
            
            string exist;
            if(unique.TryGetValue((renamedSymbol.DocumentId, renamedSymbol.Span), out exist!)) {
                if(exist == renamedSymbol.NewName) {
                    Tick();
                    continue;
                } else {
                    var doc = Common.Project.GetDocument(renamedSymbol.DocumentId);
                    var originalText = (await doc!.GetTextAsync()).GetSubText(renamedSymbol.Span);
                    Console.Error.WriteLine(
                        $"{doc.Name}:{renamedSymbol.Span} conflicts: {originalText} renamed to both {renamedSymbol.NewName} and {exist}"
                    );
                    return;
                }
            }
            unique.Add((renamedSymbol.DocumentId, renamedSymbol.Span), renamedSymbol.NewName);

            modifications.Add(
                renamedSymbol.DocumentId,
                new TextChange(renamedSymbol.Span, renamedSymbol.NewName)
            );
        }

        foreach(var (docId, changes) in modifications) {
            var doc = Common.Solution.GetDocument(docId)!;
            var text = await doc.GetTextAsync();
            var newText = text.WithChanges(changes);
            Common.Solution = Common.Solution.WithDocumentText(docId, newText);
        }
    }
}
