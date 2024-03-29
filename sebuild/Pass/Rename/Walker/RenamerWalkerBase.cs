
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace SeBuild.Pass.Rename;

/// <summary>
/// Mutable context to be shared between instances of <c>RenamerWalkerBase</c> objects
/// </summary>
class RenamerWalkerContext {
    public readonly NameGenerator NameGenerator = new NameGenerator();
    public HashSet<ISymbol> Handled = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
    public HashSet<RenamedSymbol> Renamed = new HashSet<RenamedSymbol>();
}

/// <summary>
/// Simple container struct defining a single rename action for a span in a given document
/// </summary>
struct RenamedSymbol {
    public DocumentId DocumentId;
    public TextSpan Span;
    public string NewName;
}

/// <summary>
/// A <c>CSharpSyntaxWalker</c> providing base methods to attempt renaming a symbol in a project
/// </summary>
class RenamerWalkerBase: CSharpSyntaxWalker {
    RenamerWalkerContext _ctx; 
    SemanticModel _sema;
    Solution _sln;
    List<Task> _tasks;
    
    public RenamerWalkerBase(Solution sln, RenamerWalkerContext ctx, SemanticModel sema, List<Task> tasks) {
        _ctx = ctx;
        _sln = sln;
        _sema = sema;            
        _tasks = tasks;
    }
    
    /// <summary>
    /// Get the declared symbol at the given <paramref name="node"/> and attempt to rename it
    /// </summary>
    protected void AttemptRename(SyntaxNode node) {
        var symbol = _sema
            .GetDeclaredSymbol(node)
            ?? throw new Exception($"Failed to get symbol for syntax {node.GetText()}");

        AttemptRename(symbol, _ctx.NameGenerator.Next());
    }
    
    /// <summary>
    /// Attempt to rename the given symbol, finding all references to the <paramref name="symbol"/> and submitting text changes to the source
    /// </summary>
    protected void AttemptRename(ISymbol symbol, string newName) {
        if(CanRenameSymbol(symbol)) {
            _ctx.Handled.Add(symbol);
            _tasks.Add(Task.Run(async () => {
                var AddReferences = async (IEnumerable<ReferencedSymbol> symbolReferences) => {
                    foreach(var reference in symbolReferences) {
                        foreach(var loc in reference.Locations) {
                            if(!loc.Location.IsInSource || loc.IsImplicit) { continue; }

                            var node = (await loc.Location.SourceTree!.GetRootAsync()).FindNode(loc.Location.SourceSpan);
                            if(node is ConstructorInitializerSyntax) { continue; }
                            lock(_ctx.Renamed) {
                                _ctx.Renamed.Add(new RenamedSymbol() {
                                    DocumentId = loc.Document.Id,
                                    Span = loc.Location.SourceSpan,
                                    NewName = newName
                                });
                            }
                        }
                        
                        if(!_ctx.Handled.Contains(reference.Definition)) {
                            AttemptRename(reference.Definition, newName);
                        }
                    }
                };

                var AddLocations = (IEnumerable<Location> locs) => {
                    foreach(var loc in locs) {
                        if(!loc.IsInSource) { continue; }
                        var doc = _sln.GetDocumentId(loc.SourceTree)!;
                        lock(_ctx.Renamed) {
                            _ctx.Renamed.Add(new RenamedSymbol() {
                                DocumentId = doc,
                                Span = loc.SourceSpan,
                                NewName = newName
                            });
                        }
                    }
                };

                AddLocations(symbol.Locations);

                switch(symbol) {
                    case INamedTypeSymbol decl: {
                        foreach(var ctor in decl.Constructors) {
                            AddLocations(ctor.Locations);
                            await AddReferences(await SymbolFinder.FindReferencesAsync(ctor, _sln));
                        }

                        if(decl.TypeKind == TypeKind.Interface) {
                            foreach(var member in decl.GetMembers()) {
                                AttemptRename(member, _ctx.NameGenerator.Next());
                            }
                        }
                    } break;
                };

                await AddReferences(await SymbolFinder.FindReferencesAsync(symbol, _sln));
            }));
            return;
        }
    }

    private bool CanRenameSymbol(ISymbol symbol) {
        if(symbol.Kind != SymbolKind.Namespace && !symbol.IsImplicitlyDeclared && !symbol.IsExtern && symbol.CanBeReferencedByName) {
            if(symbol.Locations.Any(loc => loc.IsInSource)) {
                if(!_ctx.Handled.Contains(symbol)) {
                    if(symbol is INamedTypeSymbol named) {
                        return !named.Name.Equals("Program");
                    } else if(symbol is IMethodSymbol method) {
                        return !(method.Name.Equals("Save") || method.Name.Equals("Main"));
                    } else {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    
}

