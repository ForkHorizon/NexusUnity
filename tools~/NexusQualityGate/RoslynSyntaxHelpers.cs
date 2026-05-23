using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NexusQualityGate;

internal static class RoslynSyntaxHelpers
{
    public static int GetDeclarationLineCount(SyntaxTree tree, SyntaxNode node)
    {
        FileLinePositionSpan span = tree.GetLineSpan(node.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    public static int GetStartLine(SyntaxTree tree, SyntaxNode node)
    {
        return tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
    }

    public static string BuildCodeExcerpt(MemberDeclarationSyntax declaration)
    {
        string text = declaration.ToFullString();
        string xml = DocumentationRules.ExtractXmlDocumentation(declaration);
        if (!string.IsNullOrWhiteSpace(xml))
            text = text.Replace(xml, string.Empty, StringComparison.Ordinal);

        text = text.Trim();
        return text.Length <= 2200 ? text : text[..2200] + "\n...";
    }

    public static string BuildSignature(MemberDeclarationSyntax declaration)
    {
        string text = declaration.ToString();
        int braceIndex = text.IndexOf('{', StringComparison.Ordinal);
        int arrowIndex = text.IndexOf("=>", StringComparison.Ordinal);
        int cutIndex = braceIndex >= 0 && arrowIndex >= 0 ? Math.Min(braceIndex, arrowIndex) : Math.Max(braceIndex, arrowIndex);
        if (cutIndex > 0)
            text = text[..cutIndex].TrimEnd() + ";";

        string[] lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("///", StringComparison.Ordinal))
            .ToArray();
        string signature = string.Join(" ", lines);
        return signature.Length <= 500 ? signature : signature[..500] + "...";
    }

    public static string GetMemberName(MemberDeclarationSyntax declaration)
    {
        return declaration switch
        {
            NamespaceDeclarationSyntax ns => ns.Name.ToString(),
            FileScopedNamespaceDeclarationSyntax ns => ns.Name.ToString(),
            TypeDeclarationSyntax type => GetFullTypeName(type),
            EnumDeclarationSyntax enumDeclaration => GetFullTypeName(enumDeclaration),
            DelegateDeclarationSyntax del => del.Identifier.Text,
            MethodDeclarationSyntax method => method.Identifier.Text,
            ConstructorDeclarationSyntax ctor => ctor.Identifier.Text,
            DestructorDeclarationSyntax dtor => dtor.Identifier.Text,
            OperatorDeclarationSyntax op => op.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type.ToString(),
            PropertyDeclarationSyntax property => property.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier.Text,
            FieldDeclarationSyntax field => string.Join(", ", field.Declaration.Variables.Select(variable => variable.Identifier.Text)),
            EventFieldDeclarationSyntax eventField => string.Join(", ", eventField.Declaration.Variables.Select(variable => variable.Identifier.Text)),
            _ => declaration.Kind().ToString(),
        };
    }

    public static string GetFullTypeName(BaseTypeDeclarationSyntax type)
    {
        Stack<string> names = new();
        SyntaxNode? node = type;
        while (node is not null)
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax nestedType:
                    names.Push(nestedType.Identifier.Text);
                    break;
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    names.Push(namespaceDeclaration.Name.ToString());
                    break;
                case FileScopedNamespaceDeclarationSyntax namespaceDeclaration:
                    names.Push(namespaceDeclaration.Name.ToString());
                    break;
            }

            node = node.Parent;
        }

        return string.Join(".", names);
    }
}

internal sealed record SourceFile(string AbsolutePath, string RelativePath, string Text, SyntaxTree Tree)
{
    public CompilationUnitSyntax Root => Tree.GetCompilationUnitRoot();
}
