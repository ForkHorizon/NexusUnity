using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NexusQualityGate;

public sealed class RoslynQualityScanner
{
    private readonly QualityGateOptions _options;

    public RoslynQualityScanner(QualityGateOptions options)
    {
        _options = options;
    }

    public QualityGateResult Scan()
    {
        var result = new QualityGateResult();
        List<SourceFile> files = EnumerateSourceFiles().Select(ParseSourceFile).ToList();
        Dictionary<string, bool> partialTypeHasDocumentation = BuildPartialDocumentationMap(files);
        HashSet<string> reportedPartialTypes = new(StringComparer.Ordinal);

        foreach (SourceFile file in files)
        {
            CheckFileLength(file, result);
            CheckSyntax(file, result);
            CheckMethodLengths(file, result);
            CheckDocumentation(file, result, partialTypeHasDocumentation, reportedPartialTypes);
        }

        return result;
    }

    private IEnumerable<string> EnumerateSourceFiles()
    {
        foreach (string sourceRoot in new[] { "Editor", "Runtime" })
        {
            string path = Path.Combine(_options.Root, sourceRoot);
            if (!Directory.Exists(path))
                continue;

            foreach (string file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (IsSkippedPath(file))
                    continue;

                yield return file;
            }
        }
    }

    private static bool IsSkippedPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/Tests/", StringComparison.Ordinal);
    }

    private SourceFile ParseSourceFile(string path)
    {
        string text = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(
            text,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
            path);

        return new SourceFile(path, Path.GetRelativePath(_options.Root, path).Replace('\\', '/'), text, tree);
    }

    private static Dictionary<string, bool> BuildPartialDocumentationMap(IEnumerable<SourceFile> files)
    {
        Dictionary<string, bool> map = new(StringComparer.Ordinal);

        foreach (SourceFile file in files)
        {
            CompilationUnitSyntax root = file.Root;
            foreach (TypeDeclarationSyntax type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!type.Modifiers.Any(SyntaxKind.PartialKeyword))
                    continue;

                string name = RoslynSyntaxHelpers.GetFullTypeName(type);
                bool hasDocumentation = !string.IsNullOrWhiteSpace(DocumentationRules.ExtractXmlDocumentation(type));
                map[name] = map.TryGetValue(name, out bool existing) ? existing || hasDocumentation : hasDocumentation;
            }
        }

        return map;
    }

    private void CheckFileLength(SourceFile file, QualityGateResult result)
    {
        int lineCount = file.Text.Split('\n').Length;
        if (lineCount > _options.FileFailureLines)
        {
            result.Add(DocumentationRules.Error(
                "NQG001",
                $"File has {lineCount} lines, exceeding the hard limit of {_options.FileFailureLines}.",
                file.RelativePath,
                1));
        }
        else if (lineCount > _options.FileWarningLines)
        {
            result.Add(DocumentationRules.Warning(
                "NQG002",
                $"File has {lineCount} lines; consider splitting before it exceeds {_options.FileFailureLines}.",
                file.RelativePath,
                1));
        }
    }

    private static void CheckSyntax(SourceFile file, QualityGateResult result)
    {
        foreach (Diagnostic diagnostic in file.Tree.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            result.Add(DocumentationRules.Error(
                "NQG010",
                "C# parse error: " + diagnostic.GetMessage(),
                file.RelativePath,
                span.StartLinePosition.Line + 1));
        }
    }

    private void CheckMethodLengths(SourceFile file, QualityGateResult result)
    {
        if (IsTestPath(file.AbsolutePath))
            return;

        foreach (BaseMethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            int lineCount = RoslynSyntaxHelpers.GetDeclarationLineCount(file.Tree, method);
            string symbol = RoslynSyntaxHelpers.GetMemberName(method);
            int line = RoslynSyntaxHelpers.GetStartLine(file.Tree, method);

            if (lineCount > _options.MethodFailureLines)
            {
                result.Add(DocumentationRules.Error(
                    "NQG011",
                    $"Method has {lineCount} lines, exceeding the hard limit of {_options.MethodFailureLines}.",
                    file.RelativePath,
                    line,
                    symbol));
            }
            else if (lineCount > _options.MethodWarningLines)
            {
                result.Add(DocumentationRules.Warning(
                    "NQG012",
                    $"Method has {lineCount} lines; consider splitting before it exceeds {_options.MethodFailureLines}.",
                    file.RelativePath,
                    line,
                    symbol));
            }
        }
    }

    private void CheckDocumentation(
        SourceFile file,
        QualityGateResult result,
        Dictionary<string, bool> partialTypeHasDocumentation,
        HashSet<string> reportedPartialTypes)
    {
        if (IsTestPath(file.AbsolutePath))
            return;

        foreach (MemberDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (!RequiresDocumentation(declaration))
                continue;

            string symbol = RoslynSyntaxHelpers.GetMemberName(declaration);
            int line = RoslynSyntaxHelpers.GetStartLine(file.Tree, declaration);
            string xml = DocumentationRules.ExtractXmlDocumentation(declaration);

            if (declaration is TypeDeclarationSyntax typeDeclaration && typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                string fullTypeName = RoslynSyntaxHelpers.GetFullTypeName(typeDeclaration);
                if (string.IsNullOrWhiteSpace(xml) && partialTypeHasDocumentation.TryGetValue(fullTypeName, out bool covered) && covered)
                    continue;

                if (string.IsNullOrWhiteSpace(xml) && !reportedPartialTypes.Add(fullTypeName))
                    continue;
            }

            if (string.IsNullOrWhiteSpace(xml))
            {
                result.Add(DocumentationRules.Error(
                    "NQG100",
                    "Public and protected types and methods must have XML documentation.",
                    file.RelativePath,
                    line,
                    symbol));
                continue;
            }

            QualityGateIssue? summaryIssue = DocumentationRules.ValidateSummary(file.RelativePath, line, symbol, xml);
            if (summaryIssue is not null)
            {
                result.Add(summaryIssue);
                continue;
            }

            string codeExcerpt = RoslynSyntaxHelpers.BuildCodeExcerpt(declaration);
            QualityGateIssue? sideEffectIssue = DocumentationRules.ValidateSideEffectDocumentation(file.RelativePath, line, symbol, xml, codeExcerpt);
            if (sideEffectIssue is not null)
                result.Add(sideEffectIssue);

            result.DocumentationCandidates.Add(new DocumentationCandidate(
                file.RelativePath,
                line,
                symbol,
                declaration.Kind().ToString(),
                RoslynSyntaxHelpers.BuildSignature(declaration),
                xml,
                codeExcerpt));
        }
    }

    private static bool RequiresDocumentation(MemberDeclarationSyntax declaration)
    {
        if (declaration is not BaseTypeDeclarationSyntax
            && declaration is not BaseMethodDeclarationSyntax
            && declaration is not DelegateDeclarationSyntax)
        {
            return false;
        }

        SyntaxTokenList modifiers = declaration switch
        {
            BaseTypeDeclarationSyntax typed => typed.Modifiers,
            BaseMethodDeclarationSyntax method => method.Modifiers,
            DelegateDeclarationSyntax del => del.Modifiers,
            _ => default,
        };

        return modifiers.Any(SyntaxKind.PublicKeyword)
            || modifiers.Any(SyntaxKind.ProtectedKeyword);
    }

}
