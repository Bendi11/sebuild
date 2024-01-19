using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace SeBuild.Pass.DeadCodeRemover;

public class DeadCodeRemover: CompilationPass {
    Dictionary<ISymbol, AliveMarker> _alive = new Dictionary<ISymbol, AliveMarker>(SymbolEqualityComparer.Default);
    DocumentId? _aliveDoc = null;

    class AliveMarker {
        public bool Alive;
        public SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        public AliveMarker(bool alive) {
            Alive = alive;
        }
    }

    public DeadCodeRemover(ScriptCommon ctx, PassProgress prog) : base(ctx, prog) {}

    public async override Task Execute() {
        await Init();

        var tasks = new List<Task>();
        var map = new Dictionary<DocumentId, SyntaxNode>();

        foreach(var docs in Common.DocumentsIter.Chunk(3)) {
            tasks.Add(Task.Run(async () => {
                foreach(var doc in docs) {
                    var syntax = await doc.GetSyntaxRootAsync() as CSharpSyntaxNode;
                    if(syntax is null) { return; }
                    
                    var removed = syntax.Accept(new DeadCodeRewriter(this, doc.Project));
                    if(removed is null) { return; }

                    lock(map) {
                        map.Add(doc.Id, removed);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        foreach(var (newDocId, newDoc) in map) {
            Common.Solution = Common
                .Solution
                .WithDocumentSyntaxRoot(newDocId, newDoc, PreservationMode.PreserveIdentity);
        }
    }
    
    /// Add alive annotation to known alive classes
    async Task Init() {
        foreach(var doc in Common.DocumentsIter) {
            var syntax = await doc.GetSyntaxRootAsync() as CSharpSyntaxNode;
            if(syntax is null) { continue; }
            var finder = new MainProgramFinder();
            syntax.Accept(finder);
            if(finder.ProgramDecl is not null) {
                var symbol = await GetSymbol(finder.ProgramDecl, doc.Project);
                if(symbol is not null) {
                    _alive.Add(symbol, new AliveMarker(true));
                    _aliveDoc = doc.Id;
                    var decl = symbol as INamedTypeSymbol;
                    if(decl is not null) {
                        foreach(var member in decl.GetMembers()) {
                            _alive.Add(member, new AliveMarker(true));
                        }
                    }
                    break;
                }
            }
        }
    }

    class MainProgramFinder: CSharpSyntaxWalker {
        public ClassDeclarationSyntax? ProgramDecl = null;

        public MainProgramFinder() {}

        public override void Visit(SyntaxNode? node) {
            if(ProgramDecl is null) { base.Visit(node); }
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node) {
            if(
                    node.Identifier.ValueText.Equals("Program") &&
                    (
                        node
                            .BaseList
                            ?.Types
                            .AsEnumerable()
                            .Any(ty => (ty.Type as IdentifierNameSyntax)?.Identifier.ValueText.Equals("MyGridProgram") ?? false)
                        ?? false
                    )
            ) {
                ProgramDecl = node;
                return;
            }
            
            base.VisitClassDeclaration(node);
        }
    }

    class DeadCodeRewriter: CSharpSyntaxRewriter {
        DeadCodeRemover _parent;
        Project _project;

        public DeadCodeRewriter(DeadCodeRemover parent, Project p) {
            _parent = parent;
            _project = p;
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) =>
            Referenced(node) ? base.VisitMethodDeclaration(node) : null;

        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) =>
            Referenced(node) ? base.VisitFieldDeclaration(node) : null;
        
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) =>
            Referenced(node) ? base.VisitPropertyDeclaration(node) : null;

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) =>
            Referenced(node) ? base.VisitClassDeclaration(node) : null;

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
            Referenced(node) ? base.VisitConstructorDeclaration(node) : null;

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) =>
            Referenced(node) ? base.VisitStructDeclaration(node) : null;

        bool Referenced(CSharpSyntaxNode node) {
            var task = Task.Run(async () => await _parent.IsSyntaxReferenced(node, _project));
            task.Wait();
            bool referenced = task.Result;
            if(!referenced) {
                _parent.Tick();
            }

            return referenced;
        }
    }

    async Task<ISymbol?> GetSymbol(SyntaxNode node, Project proj) {
        var comp = await proj.GetCompilationAsync();
        var sema = comp?.GetSemanticModel(node.SyntaxTree);
        return sema?.GetDeclaredSymbol(node);
    }

    async Task<SymbolInfo?> GetReferencedSymbol(SyntaxNode node, Project proj) {
        var comp = await proj.GetCompilationAsync();
        var sema = comp?.GetSemanticModel(node.SyntaxTree, true);
        return sema?.GetSymbolInfo(node);
    }

    async Task<bool> IsSyntaxReferenced(SyntaxNode node, Project proj) {
        var symbol = await GetSymbol(node, proj);
        return symbol is null ? true : await IsSymbolReferenced(symbol, true) == ReferencedResult.Referenced;
    }

    enum ReferencedResult {
        Referenced,
        NotReferenced,
        Circular,
    }
    
    async Task<ReferencedResult> IsSymbolReferenced(ISymbol sym, bool forceWait) {
        AliveMarker? marker;
        bool exists = false;

        Monitor.Enter(_alive);
            exists = _alive.TryGetValue(sym, out marker);
            if(exists && marker is not null) {
                Monitor.Exit(_alive);
                if(!forceWait && marker!.Semaphore.CurrentCount == 0) {
                    return ReferencedResult.Circular;
                }

                await marker.Semaphore.WaitAsync();
                try {
                    return marker.Alive ? ReferencedResult.Referenced : ReferencedResult.NotReferenced;
                } finally { marker.Semaphore.Release(); }
            }

            marker = new AliveMarker(false);
            marker.Semaphore.Wait();
            _alive.TryAdd(sym, marker);
        Monitor.Exit(_alive);
        

        try {
            var refs = await SymbolFinder.FindReferencesAsync(sym, Common.Solution);
            foreach(var reference in refs) {
                foreach(var loc in reference.Locations) {
                    if(_aliveDoc is not null && loc.Document.Id == _aliveDoc) {
                        marker.Alive = true;
                        return ReferencedResult.Referenced;
                    }
                    var sourceNode = loc.Location.SourceTree;
                    if(sourceNode is null) { continue; }

                    var sourceTree = await sourceNode.GetRootAsync();

                    var node = sourceTree?.FindNode(loc.Location.SourceSpan);

                    if(node is null) { continue; }
                    ISymbol? symbol = null;

                    while(node is not null && (symbol is null || symbol is ILocalSymbol)) {
                        symbol = await GetSymbol(node, loc.Document.Project);
                        node = node.Parent;
                    }
                    
                    if(symbol is null || SymbolEqualityComparer.Default.Equals(symbol, sym)) { continue; }
                    var res = await IsSymbolReferenced(symbol, false);
                    if(res == ReferencedResult.Circular) {
                        marker.Alive = true;
                        return ReferencedResult.Referenced;
                    } else if(res == ReferencedResult.Referenced) {
                        marker.Alive = true;
                        return ReferencedResult.Referenced;
                    }
                } 
            }
        } finally { marker.Semaphore.Release(); }

        return ReferencedResult.NotReferenced;
    }
}
