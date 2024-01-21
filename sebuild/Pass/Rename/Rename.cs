using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace SeBuild.Pass.Rename;

public class Renamer: CompilationPass {
    readonly NameGenerator _gen = new NameGenerator();
    
    HashSet<ISymbol> _handled = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
    HashSet<(DocumentId, TextSpan, string)> _renames = new HashSet<(DocumentId, TextSpan, string)>();

    static readonly SymbolRenameOptions _opts = new SymbolRenameOptions() {
        RenameOverloads = true,
        RenameFile = false,
        RenameInComments = false,
        RenameInStrings = false,
    };
    

    public Renamer(ScriptCommon ctx, PassProgress prog) : base(ctx, prog) {}

    delegate RenamerWalkerBase ConstructRenamer(SemanticModel sema, List<Task> tasks);

    async Task RenameWith<T>(ConstructRenamer New) where T: RenamerWalkerBase {
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

    /// Get the next symbol to rename
    async Task Symbol() {
        await RenameWith<InterfaceRenamerWalker>((sema, tasks) => new InterfaceRenamerWalker(this, sema, tasks));
        await RenameWith<RenamerWalker>((sema, tasks) => new RenamerWalker(this, sema, tasks));
    }
    
    /// Rename all identifiers in the given project
    async public override Task Execute() {
        await Symbol();
        
        var modifications = new MultiMap<DocumentId, TextChange>();
        var unique = new Dictionary<(DocumentId, TextSpan), string>();

        foreach(var (docId, reference, name) in _renames) {
            Msg($"{reference} -> {name}");
            Tick();
            
            string exist;
            if(unique.TryGetValue((docId, reference), out exist!)) {
                if(exist == name) {
                    Msg("Skipping doubly-renamed symbol");
                    Tick();
                    continue;
                } else {
                    var doc = Common.Project.GetDocument(docId);
                    var originalText = (await doc!.GetTextAsync()).GetSubText(reference);
                    Console.Error.WriteLine(
                        $"{doc.Name}:{reference} conflicts: {originalText} renamed to both {name} and {exist}"
                    );
                    return;
                }
            }
            unique.Add((docId, reference), name);

            modifications.Add(
                docId,
                new TextChange(reference, name)
            );
        }

        foreach(var (docId, changes) in modifications) {
            var doc = Common.Solution.GetDocument(docId)!;
            var text = await doc.GetTextAsync();
            var newText = text.WithChanges(changes);
            Common.Solution = Common.Solution.WithDocumentText(docId, newText);
        }
    }

    private class RenamerWalker: RenamerWalkerBase {
        public RenamerWalker(
            Renamer parent, SemanticModel sema, List<Task> tasks
        ): base(parent, sema, tasks) {
            
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node) {
            AttemptRename(node);
            base.VisitClassDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node) {
            AttemptRename(node);
            base.VisitStructDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node) {
            AttemptRename(node);
            base.VisitEnumDeclaration(node);
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node) {
            AttemptRename(node);
            base.VisitEnumMemberDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node) {
            AttemptRename(node);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitParameter(ParameterSyntax param) {
            AttemptRename(param);
            base.VisitParameter(param);
        }

        public override void VisitTypeParameter(TypeParameterSyntax tparam) {
            AttemptRename(tparam);
            base.VisitTypeParameter(tparam);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax vbl) {
            foreach(var name in vbl.Variables) {
                AttemptRename(name); 
            }
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node) {
            AttemptRename(node);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax frch) {
            AttemptRename(frch);
            base.VisitForEachStatement(frch);
        }
    }

    private class InterfaceRenamerWalker: RenamerWalkerBase {
        public InterfaceRenamerWalker(
            Renamer parent, SemanticModel sema, List<Task> tasks
        ): base(parent, sema, tasks) {
            
        }
        
        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) {
            AttemptRename(node);
            foreach(var member in node.Members) {
                AttemptRename(member);
                base.Visit(member);
            }
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node) {
            if(node.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AbstractKeyword))) {
                AttemptRename(node);
            }
        }

    }

    private class RenamerWalkerBase: CSharpSyntaxWalker {
        public Renamer Parent;
        public SemanticModel _sema;
        public List<Task> _tasks;

        public RenamerWalkerBase(Renamer parent, SemanticModel sema, List<Task> tasks) {
            Parent = parent;
            _sema = sema;            
            _tasks = tasks;
        }

        protected virtual bool ValidReplacement(ISymbol original, ReferencedSymbol reference) {
            return true;
        }

        protected void AttemptRename(SyntaxNode node) {
            var symbol = _sema
                .GetDeclaredSymbol(node)
                ?? throw new Exception($"Failed to get symbol for syntax {node.GetText()}");

            AttemptRename(symbol, Parent._gen.Next());
        }

        protected void AttemptRename(ISymbol symbol, string newName) {
            if(symbol.Kind != SymbolKind.Namespace &&
                !Parent._handled.Contains(symbol) &&
                !symbol.IsImplicitlyDeclared &&
                symbol.Locations.Any((loc) => loc.IsInSource) &&
                !symbol.IsExtern &&
                symbol.CanBeReferencedByName &&
                !(symbol is INamedTypeSymbol && (symbol.Name.Equals("Program"))) &&
                !(symbol is IMethodSymbol && (symbol.Name.Equals("Save") || symbol.Name.Equals("Main")))
            ) {
                Parent._handled.Add(symbol);
                _tasks.Add(Task.Run(async () => {
                    var AddReferences = async (IEnumerable<ReferencedSymbol> symbolReferences) => {
                        foreach(var reference in symbolReferences) {
                            if(!ValidReplacement(symbol, reference)) { continue; }
                            foreach(var loc in reference.Locations) {
                                if(!loc.Location.IsInSource || loc.IsImplicit) { continue; }

                                var node = (await loc.Location.SourceTree!.GetRootAsync()).FindNode(loc.Location.SourceSpan);
                                if(node is ConstructorInitializerSyntax) { continue; }
                                lock(Parent._renames) { Parent._renames.Add((loc.Document.Id, loc.Location.SourceSpan, newName)); }
                            }
                            
                            if(!Parent._handled.Contains(reference.Definition)) {
                                AttemptRename(reference.Definition, newName);
                            }
                        }
                    };

                    var AddLocations = (IEnumerable<Location> locs) => {
                        foreach(var loc in locs) {
                            if(!loc.IsInSource) { continue; }
                            var doc = Parent.Common.Solution.GetDocumentId(loc.SourceTree)!;
                            lock(Parent._renames) { Parent._renames.Add((doc, loc.SourceSpan, newName)); }
                        }
                    };

                    AddLocations(symbol.Locations);

                    switch(symbol) {
                        case INamedTypeSymbol decl: {
                            foreach(var ctor in decl.Constructors) {
                                AddLocations(ctor.Locations);
                                await AddReferences(await SymbolFinder.FindReferencesAsync(ctor, Parent.Common.Solution));
                            }

                            if(decl.TypeKind == TypeKind.Interface) {
                                foreach(var member in decl.GetMembers()) {
                                    AttemptRename(member, Parent._gen.Next());
                                }
                            }
                        } break;
                    };

                    await AddReferences(await SymbolFinder.FindReferencesAsync(symbol, Parent.Common.Solution));
                }));
                return;
            }
        }
    }

        }
}
