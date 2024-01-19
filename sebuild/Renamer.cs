using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace SeBuild;

public class Renamer: CompilationPass {
    readonly NameGenerator _gen = new NameGenerator();
    
    HashSet<ISymbol> _handled = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
    HashSet<(DocumentId, TextSpan, string)> _renames = new HashSet<(DocumentId, TextSpan, string)>();

    class MultiMap<K, V>: IEnumerable<KeyValuePair<K, HashSet<V>>>
    where K: notnull {
        Dictionary<K, HashSet<V>> _multiMap;


        public MultiMap(System.Collections.Generic.IEqualityComparer<K>? cmp = null) {
            _multiMap = 
                new Dictionary<K, HashSet<V>>(cmp);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<KeyValuePair<K, HashSet<V>>> GetEnumerator() =>
            _multiMap.GetEnumerator();

        public bool Contains(K symbol) =>
            _multiMap.ContainsKey(symbol);

        public void Add(K symbol, V reference) {
            HashSet<V>? references = null;
            if(_multiMap.TryGetValue(symbol, out references)) {
                references.Add(reference);
            } else {
                references = new HashSet<V>();
                references.Add(reference);
                _multiMap[symbol] = references;
            }
        }
        
        /// Remove all references for the given symbol, without removing it from the map
        public void Clear(K symbol) =>
            _multiMap[symbol] = new HashSet<V>();
        
        /// Remap the `from` key to track changes to the `to` key's list
        public void Remap(K from, K to) => _multiMap[from] = _multiMap[to];

        public HashSet<V>? Get(K symbol) {
            HashSet<V>? value = null;
            _multiMap.TryGetValue(symbol, out value);
            return value;
        }
    }

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

    /// Generator for creating random unicode names
    private struct NameGenerator {
        List<IEnumerator<char>> _gen = new List<IEnumerator<char>>();

        public NameGenerator() {
            _gen.Add(UnicodeEnumerator());
        }

        private void IncrementSlot(int slot) {
            if(slot >= _gen.Count) {
                _gen.Append(UnicodeEnumerator());
                return;
            }

            if(!_gen[slot].MoveNext()) {
                _gen[slot] = UnicodeEnumerator();
                IncrementSlot(slot + 1);
            }
        }

        public string Next() {
            StringBuilder sb = new StringBuilder();
            
            IncrementSlot(0);
            foreach(var slot in _gen) {
                sb.Append(slot.Current);
            }
            
            return sb.ToString();
        }

        IEnumerator<char> UnicodeEnumerator() {
            IEnumerable<char> Range(int from, int to) {
                for(int i = from; i <= to; ++i) { yield return (char)i; }
            }
            
            var letters = Range(0x41, 0x5A)
                .Concat(Range(0x61, 0x7A))
                .Concat(Range(0xC0, 0xD6))
                .Concat(Range(0xD8, 0xF6))
                //.Concat(Range(0xC0, 0xF6))
                .Concat(Range(0x100, 0x17F))
                .Concat(Range(0x180, 0x1BF))
                .Concat(Range(0x1C4, 0x1CC))
                .Concat(Range(0x1CD, 0x1DC))
                .Concat(Range(0x1DD, 0x1FF))
                .Concat(Range(0x200, 0x217))
                .Concat(Range(0x218, 0x21B))
                .Concat(Range(0x21C, 0x24F))
                .Concat(Range(0x22A, 0x233))
                .Concat(Range(0x234, 0x236))
                .Concat(Range(0x238, 0x240))
                .Concat(Range(0x23A, 0x23E))
                .Concat(Range(0x250, 0x2A8))
                .Concat(Range(0x2A9, 0x2AD))
                .Concat(Range(0x2AE, 0x2AF))
                .Concat(Range(0x370, 0x3FB))
                .Concat(Range(0x37B, 0x37D))
                .Concat(Range(0x37F, 0x3F3))
                .Concat(Range(0x3CF, 0x3F9))
                .Concat(Range(0x3E2, 0x3EF))
                .Concat(Range(0x400, 0x45F))
                .Concat(Range(0x410, 0x44F))
                .Concat(Range(0x460, 0x481))
                .Concat(Range(0x48A, 0x4F9))
                .Concat(Range(0x4FA, 0x4FF))
                .Concat(Range(0x500, 0x52D))
                .Concat(Range(0x531, 0x556))
                .Concat(Range(0x560, 0x588))
                .Concat(Range(0x10A0, 0x10C5))
                .Concat(Range(0x10D0, 0x10F0))
                .Concat(Range(0x13A0, 0x13F4))
                .Concat(Range(0x1C90, 0x1CB0))
                .Concat(Range(0x1E00, 0x1EF9))
                .Concat(Range(0x1EA0, 0x1EF1))
                .Concat(Range(0x1F00, 0x1FFC))
                .Concat(Range(0x2C00, 0x2C2E))
                .Concat(Range(0x2C30, 0x2C5E));

            foreach(var letter in letters) { yield return letter; }
        }
    }
}
