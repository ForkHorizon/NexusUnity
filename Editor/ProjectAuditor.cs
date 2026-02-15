using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// A deterministic C# linter using Roslyn (Microsoft.CodeAnalysis) for deep structural analysis.
    /// Monitors file length, method complexity, nesting depth, and naming conventions.
    /// </summary>
    public static class ProjectAuditor
    {
        private const int _MAX_FILE_LINES = 300;
        private const int _MAX_METHOD_LINES = 40;
        private const int _MAX_METHOD_PARAMS = 5;
        private const int _MAX_NESTING_DEPTH = 5;
        private const int _MAX_COMPLEXITY = 10;

        private static List<string> _violations = new List<string>();

        /// <summary>
        /// Entry point for the Unity Menu Item.
        /// </summary>
        [MenuItem("Tools/Nexus/Run Linter (whole proj)")]
        public static void RunAuditMenu()
        {
            RunAudit(silent: false);
        }

        /// <summary>
        /// Runs a full audit on the project's Assets folder and generates LINT_REPORT.txt.
        /// </summary>
        /// <param name="silent">If true, does not show a UI dialog on completion.</param>
        /// <returns>The content of the generated report.</returns>
        public static string RunAudit(bool silent = true)
        {
            _violations.Clear();
            var files = GetProjectFiles();

            foreach (var file in files)
            {
                AnalyzeFile(file);
            }

            string reportContent = GenerateReport();
            ShowCompletionFeedback(silent);

            return reportContent;
        }

        private static string[] GetProjectFiles()
        {
            string assetsPath = Application.dataPath;
            return Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/Plugins/") && !f.Contains("/External/") && !f.Contains("/References/"))
                .ToArray();
        }

        private static string GenerateReport()
        {
            string reportContent = _violations.Count > 0 
                ? string.Join("\n", _violations) 
                : "No violations found. 100% Compliance.";

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LINT_REPORT.txt");
            File.WriteAllText(reportPath, reportContent);
            return reportContent;
        }

        private static void ShowCompletionFeedback(bool silent)
        {
            bool success = _violations.Count == 0;
            if (!silent)
            {
                string title = success ? "Linter Passed" : "Linter Violations Found";
                string message = success 
                    ? "100% Compliance! No violations found." 
                    : $"Found {_violations.Count} violations. Results have been written to LINT_REPORT.txt";
                EditorUtility.DisplayDialog(title, message, "OK");
            }

            if (!success)
                Debug.LogWarning($"[Auditor] Audit complete. Found {_violations.Count} violations. See LINT_REPORT.txt");
            else
                Debug.Log("[Auditor] Audit complete. 100% Compliance.");
        }

        private static void AnalyzeFile(string filePath)
        {
            string relPath = filePath.Replace(Application.dataPath, "Assets");
            string code = File.ReadAllText(filePath);
            
            int lineCount = code.Split('\n').Length;
            if (lineCount > _MAX_FILE_LINES)
            {
                _violations.Add($"{relPath}:0: [File] length is {lineCount} lines (Limit: {_MAX_FILE_LINES})");
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var walker = new AuditWalker(relPath, _violations);
            walker.Visit(root);
        }

        /// <summary>
        /// Main syntax walker for detecting linter violations.
        /// </summary>
        private class AuditWalker : CSharpSyntaxWalker
        {
            private readonly string _path;
            private readonly List<string> _violations;

            /// <summary>
            /// Initializes a new instance of the AuditWalker.
            /// </summary>
            /// <param name="path">The relative path of the file being audited.</param>
            /// <param name="violations">The list to collect violations into.</param>
            public AuditWalker(string path, List<string> violations) : base(SyntaxWalkerDepth.Node)
            {
                _path = path;
                _violations = violations;
            }

            /// <summary>
            /// Analyzes method declarations for length, parameters, and complexity.
            /// </summary>
            /// <param name="node">The method syntax node.</param>
            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                int lineNum = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                string methodName = node.Identifier.Text;

                var lineSpan = node.GetLocation().GetLineSpan();
                int methodLength = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
                if (methodLength > _MAX_METHOD_LINES)
                    _violations.Add($"{_path}:{lineNum}: [Method] '{methodName}' is {methodLength} lines (Limit: {_MAX_METHOD_LINES})");

                if (node.ParameterList.Parameters.Count > _MAX_METHOD_PARAMS)
                    _violations.Add($"{_path}:{lineNum}: [Parameters] '{methodName}' has {node.ParameterList.Parameters.Count} parameters (Limit: {_MAX_METHOD_PARAMS})");

                int complexity = CalculateComplexity(node);
                if (complexity > _MAX_COMPLEXITY)
                    _violations.Add($"{_path}:{lineNum}: [Complexity] '{methodName}' is {complexity} (Limit: {_MAX_COMPLEXITY})");

                base.VisitMethodDeclaration(node);
            }

            /// <summary>
            /// Checks nesting depth of blocks.
            /// </summary>
            /// <param name="node">The block syntax node.</param>
            public override void VisitBlock(BlockSyntax node)
            {
                int depth = 0;
                SyntaxNode parent = node.Parent;
                while (parent != null)
                {
                    if (parent is BlockSyntax) depth++;
                    parent = parent.Parent;
                }

                if (depth > _MAX_NESTING_DEPTH)
                {
                    int lineNum = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    _violations.Add($"{_path}:{lineNum}: [Nesting] depth is {depth} (Limit: {_MAX_NESTING_DEPTH})");
                }

                base.VisitBlock(node);
            }

            /// <summary>
            /// Validates naming conventions for private fields.
            /// </summary>
            /// <param name="node">The field declaration syntax node.</param>
            public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                // Skip if not private OR if it is a constant/readonly (constants use UPPER_CASE)
                bool isPrivate = node.Modifiers.Any(SyntaxKind.PrivateKeyword);
                bool isConst = node.Modifiers.Any(SyntaxKind.ConstKeyword);
                bool isReadOnly = node.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

                if (!isPrivate || isConst || isReadOnly) 
                {
                    base.VisitFieldDeclaration(node);
                    return;
                }

                foreach (var variable in node.Declaration.Variables)
                {
                    string name = variable.Identifier.Text;
                    if (!name.StartsWith("_"))
                    {
                        int lineNum = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        _violations.Add($"{_path}:{lineNum}: [Naming] Private field '{name}' should start with '_'");
                    }
                }
                base.VisitFieldDeclaration(node);
            }

            private int CalculateComplexity(MethodDeclarationSyntax node)
            {
                var complexityWalker = new ComplexityWalker();
                complexityWalker.Visit(node);
                return complexityWalker.Complexity;
            }
        }

        /// <summary>
        /// Calculates cyclomatic complexity by visiting branching syntax nodes.
        /// </summary>
        private class ComplexityWalker : CSharpSyntaxWalker
        {
            /// <summary>
            /// Gets the calculated complexity score.
            /// </summary>
            public int Complexity { get; private set; } = 1;

            /// <summary>Increments complexity for if statements.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitIfStatement(IfStatementSyntax node) { Complexity++; base.VisitIfStatement(node); }
            /// <summary>Increments complexity for while statements.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitWhileStatement(WhileStatementSyntax node) { Complexity++; base.VisitWhileStatement(node); }
            /// <summary>Increments complexity for do statements.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitDoStatement(DoStatementSyntax node) { Complexity++; base.VisitDoStatement(node); }
            /// <summary>Increments complexity for for statements.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitForStatement(ForStatementSyntax node) { Complexity++; base.VisitForStatement(node); }
            /// <summary>Increments complexity for foreach statements.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitForEachStatement(ForEachStatementSyntax node) { Complexity++; base.VisitForEachStatement(node); }
            /// <summary>Increments complexity for switch sections.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitSwitchSection(SwitchSectionSyntax node) { Complexity++; base.VisitSwitchSection(node); }
            /// <summary>Increments complexity for catch clauses.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitCatchClause(CatchClauseSyntax node) { Complexity++; base.VisitCatchClause(node); }
            
            /// <summary>Increments complexity for logical binary expressions.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                if (node.Kind() == SyntaxKind.LogicalAndExpression || 
                    node.Kind() == SyntaxKind.LogicalOrExpression ||
                    node.Kind() == SyntaxKind.CoalesceExpression)
                {
                    Complexity++;
                }
                base.VisitBinaryExpression(node);
            }

            /// <summary>Increments complexity for conditional (ternary) expressions.</summary>
            /// <param name="node">The syntax node.</param>
            public override void VisitConditionalExpression(ConditionalExpressionSyntax node) { Complexity++; base.VisitConditionalExpression(node); }
        }
    }
}
