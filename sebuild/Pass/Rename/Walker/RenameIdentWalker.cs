
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SeBuild.Pass.Rename;

/// <summary>
/// Syntax walker that will rename all declarations, fields, and methods in the syntax it walks.
/// Made to run AFTER the <c>InterfaceRenamerWalker</c> to ensure symbols are renamed in the correct fashion
/// </summary>
class RenameIdentWalker: RenamerWalkerBase {
    public RenameIdentWalker(Solution sln, RenamerWalkerContext ctx, SemanticModel sema, List<Task> tasks)
        : base(sln, ctx, sema, tasks) {}

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

