
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SeBuild.Pass.Rename;

/// <summary>
/// Syntax walker that only renames the interface declarations it comes across, must run before the
/// <c>RenameIdentWalker</c> in order to properly find references to interface methods
/// </summary>
class InterfaceRenamerWalker: RenamerWalkerBase {
    public InterfaceRenamerWalker(Solution sln, RenamerWalkerContext ctx, SemanticModel sema, List<Task> tasks)
        : base(sln, ctx, sema, tasks) {}
    
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
