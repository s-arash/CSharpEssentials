using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CSharpEssentials.NullCheckToNullConditional
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullCheckToNullConditionalAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DiagnosticDescriptors.UseNullConditionalMemberAccessFadedToken, DiagnosticDescriptors.UseNullConditionalMemberAccess);

        private static async void AnalyzeThat(SyntaxNodeAnalysisContext context)
        {
            var ifStatement = context.Node.FindNode(context.Node.Span, getInnermostNodeForTie: true)?.FirstAncestorOrSelf<IfStatementSyntax>();
            try
            {
                if (await NullCheckToNullConditionalCodeFix.GetCodeFixAsync(() => Task.FromResult(context.SemanticModel), ifStatement) != null)
                {
                    if (ifStatement.SyntaxTree.IsGeneratedCode(context.CancellationToken))
                        return;
                    var fadeoutLocations = ImmutableArray.CreateBuilder<Location>();
                    fadeoutLocations.Add(Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(ifStatement.IfKeyword.SpanStart, ifStatement.Statement.SpanStart)));

                    var statementBlock = ifStatement.Statement as BlockSyntax;
                    if (statementBlock != null)
                    {
                        fadeoutLocations.Add(Location.Create(context.Node.SyntaxTree, (statementBlock.OpenBraceToken.Span)));
                        fadeoutLocations.Add(Location.Create(context.Node.SyntaxTree, (statementBlock.CloseBraceToken.Span)));
                    }
                    foreach (var location in fadeoutLocations)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseNullConditionalMemberAccessFadedToken, location));
                    }
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseNullConditionalMemberAccess,
                        Location.Create(context.Node.SyntaxTree, ifStatement.Span)));
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == context.CancellationToken)
            {
                // we should ignore cancellation exceptions, instead of blowing up the universe!
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeThat, ImmutableArray.Create(SyntaxKind.IfStatement));
        }
    }
}
