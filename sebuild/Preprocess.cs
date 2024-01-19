using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SeBuild;

/// <summary>
/// Builds a single .csproj project: resolves project references, removes header / footer code, and returns the
/// final declarations list
/// </summary>
public class Preprocessor {
    ScriptCommon Common;
    ConcurrentBag<CSharpSyntaxNode> decls;
    PassProgress _prog;

    async public static Task<IEnumerable<CSharpSyntaxNode>> Build(ScriptCommon ctx, PassProgress prog)
        => await new Preprocessor(ctx, prog).Finish(); 

    private Preprocessor(ScriptCommon ctx, PassProgress prog, ConcurrentBag<CSharpSyntaxNode>? dec = null) {
        Common = ctx;
        _prog = prog;
        decls = dec ?? new ConcurrentBag<CSharpSyntaxNode>();
    }
    
    private class PreprocessWalker: CSharpSyntaxWalker {
        ConcurrentBag<CSharpSyntaxNode> decls;
        PassProgress _prog;
        public PreprocessWalker(ConcurrentBag<CSharpSyntaxNode> dec, PassProgress prog) : base(SyntaxWalkerDepth.Node) {
            decls = dec;
            _prog = prog;
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node) {
            decls.Add(node);
            _prog.Report(1);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node) {
            decls.Add(node);
            _prog.Report(1);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) {
            decls.Add(node);
            _prog.Report(1);
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
                foreach(var member in node.Members) {
                    decls.Add(member);
                    _prog.Report(1);
                }
            } else {
                decls.Add(node);
                _prog.Report(1);
            }
        }
    }

    async private Task<IEnumerable<CSharpSyntaxNode>> Finish() {
        var tasks = new List<Task>();
        foreach(var doc in from doc in Common.DocumentsIter where doc.Folders.FirstOrDefault() != "obj" select doc) {
            tasks.Add(Task.Run(async () => await Digest(doc)));
        }

        await Task.WhenAll(tasks);
        
        return this.decls;
    }

    async private Task Digest(Document doc) {
        var syntax = await doc.GetSyntaxTreeAsync() as CSharpSyntaxTree ?? throw new Exception("Cannot compile non-C# files");
        syntax
            .GetRoot()
            .Accept(new PreprocessWalker(decls, _prog));
    }
}
