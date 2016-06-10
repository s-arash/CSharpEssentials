using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;

namespace CSharpEssentials.NullCheckToNullConditional
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "Use null-conditional operator")]
    class NullCheckToNullConditionalCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.UseNullConditional);
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            if (context.Diagnostics.Length == 0) return;

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            foreach (var diag in context.Diagnostics)
            {
                var ifStatement = diag.Location.SourceTree.GetRoot().FindNode(diag.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<IfStatementSyntax>();
                context.RegisterCodeFix(CodeAction.Create("Replace null-check 'if' with null-conditional member access", async ct =>
                {
                    var expressionToReplace = await GetCodeFixAsync(() => context.Document.GetSemanticModelAsync(ct), ifStatement);
                    var newSyntax = root.ReplaceNode(ifStatement, expressionToReplace);
                    return context.Document.WithSyntaxRoot(newSyntax);
                }), context.Diagnostics);
            }
        }

        internal static async Task<ExpressionStatementSyntax> GetCodeFixAsync(Func<Task<SemanticModel>> semanticModelLazy, IfStatementSyntax ifStatement)
        {
            if (ifStatement != null && ifStatement.Else == null)
            {
                var binaryExpression = ifStatement.Condition as BinaryExpressionSyntax;
                if (binaryExpression?.IsKind(SyntaxKind.NotEqualsExpression) == true)
                {
                    ExpressionSyntax nullableExpression = null;
                    if (binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression))
                        nullableExpression = binaryExpression.Right;
                    else if (binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression))
                        nullableExpression = binaryExpression.Left;

                    if (nullableExpression != null)
                    {
                        var block = ifStatement.Statement as BlockSyntax;
                        var bodyExpressionStatementOriginal = (block?.Statements.Count == 1) ?
                             block.Statements[0] as ExpressionStatementSyntax : ifStatement.Statement as ExpressionStatementSyntax;
                        var bodyExpressionStatement = bodyExpressionStatementOriginal;
                        if (bodyExpressionStatement != null)
                        {
                            var invocation = bodyExpressionStatement.Expression as InvocationExpressionSyntax;
                            ExpressionSyntax chainStart;
                            if (invocation != null && MemberAccessChainExpressionStartsWith(invocation, nullableExpression, out chainStart))
                            {
                                var semanticModel = await semanticModelLazy();
                                var referenceType = semanticModel.GetTypeInfo(nullableExpression).Type?.IsReferenceType;
                                if (referenceType == null) return null;
                                if (referenceType == false)
                                {
                                    var chainStartParentMemberAccess = chainStart.Parent as MemberAccessExpressionSyntax;
                                    if (chainStartParentMemberAccess != null)
                                    {
                                        if (chainStartParentMemberAccess.Name.Identifier.ValueText == "Value")
                                        {
                                            var InvocationValueRemoved = invocation.ReplaceNode(chainStartParentMemberAccess, nullableExpression);
                                            bodyExpressionStatement = bodyExpressionStatement.ReplaceNode(invocation, InvocationValueRemoved);
                                            ExpressionSyntax newChainStart;
                                            if (!MemberAccessChainExpressionStartsWith(bodyExpressionStatement.Expression, chainStart, out newChainStart))
                                                return null;
                                            chainStart = newChainStart;
                                        }
                                    }
                                }
                                var nullableExpressionMemberCall = GetPropertyIndexerMethodCallExpression(chainStart);
                                var nullableExpressionNullConditionalMemberCall = ConvertToNullConditionalAccess(nullableExpressionMemberCall);
                                if (nullableExpressionNullConditionalMemberCall != null)
                                {
                                    var bodyExpressionStatementTrailingTrivia = bodyExpressionStatementOriginal.GetTrailingTrivia();
                                    var ifStatementTrailingTrivia = ifStatement.GetTrailingTrivia().Except(bodyExpressionStatementTrailingTrivia);
                                    return bodyExpressionStatement
                                        .ReplaceNode(nullableExpressionMemberCall, nullableExpressionNullConditionalMemberCall)
                                        .WithLeadingTrivia(ifStatement.GetLeadingTrivia().AddRange(nullableExpressionMemberCall.GetLeadingTrivia()))
                                        .WithTrailingTrivia(nullableExpressionMemberCall.GetTrailingTrivia().AddRange(bodyExpressionStatementTrailingTrivia).AddRange(block?.CloseBraceToken.LeadingTrivia ?? Enumerable.Empty<SyntaxTrivia>()).AddRange(ifStatementTrailingTrivia))
                                        .WithAdditionalAnnotations(Formatter.Annotation);
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// returns the method, indexer, or property access on this expression, if there is any 
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        private static ExpressionSyntax GetPropertyIndexerMethodCallExpression(ExpressionSyntax exp)
        {
            if (exp.Parent is MemberAccessExpressionSyntax && exp.Parent.Parent is InvocationExpressionSyntax)
            {
                return exp.Parent.Parent as InvocationExpressionSyntax;
            }
            if (exp.Parent is MemberAccessExpressionSyntax)
            {
                return exp.Parent as MemberAccessExpressionSyntax;
            }
            if (exp.Parent is ElementAccessExpressionSyntax)
            {
                return exp.Parent as ElementAccessExpressionSyntax;
            }
            return null;
        }
        /// <summary>
        /// converts a normal method, property, or indexer access syntax to the null-conditional version
        /// </summary>
        /// <returns>
        /// the converted version of a method, property, or indexer access syntax; 
        /// or null if the argument is none of them.
        /// </returns>
        private static ExpressionSyntax ConvertToNullConditionalAccess(ExpressionSyntax exp)
        {
            if (exp is ConditionalAccessExpressionSyntax)
            {
                return exp;
            }
            else if (exp is InvocationExpressionSyntax)
            {
                var invocation = exp as InvocationExpressionSyntax;
                var memberAccess = (invocation.Expression as MemberAccessExpressionSyntax);
                if (memberAccess != null)
                {
                    return ConditionalAccessExpression(memberAccess.Expression,
                        InvocationExpression(MemberBindingExpression(memberAccess.Name), invocation.ArgumentList));
                }
                else
                {
                    return null;
                }
            }
            else if (exp is ElementAccessExpressionSyntax)
            {
                var elementAccess = exp as ElementAccessExpressionSyntax;
                return ConditionalAccessExpression(elementAccess.Expression,
                    ElementBindingExpression(elementAccess.ArgumentList));
            }
            else if (exp is MemberAccessExpressionSyntax)
            {
                var memberAccess = exp as MemberAccessExpressionSyntax;
                return ConditionalAccessExpression(memberAccess.Expression,
                    MemberBindingExpression(memberAccess.Name));
            }
            else return null;
        }

        /// <summary>
        /// determines whether 'beginning' appears at the start of 'memberAccessChain',
        /// for example 'a.b(1)[3]' is considered to be at the start of 'a.b(1)[3].c.m()'
        /// </summary>
        /// <param name="atTheBeginning"> if this method returns true, this parameter will hold the expression at the beginning of memberAccessChain that is equivalent to beginning</param>
        /// <returns></returns>
        public static bool MemberAccessChainExpressionStartsWith(ExpressionSyntax memberAccessChain, ExpressionSyntax beginning, out ExpressionSyntax atTheBeginning)
        {
            if (AreEquivalent(memberAccessChain, beginning, false))
            {
                atTheBeginning = memberAccessChain;
                return true;
            }
            switch (memberAccessChain.Kind())
            {
                case SyntaxKind.InvocationExpression:
                    return MemberAccessChainExpressionStartsWith((memberAccessChain as InvocationExpressionSyntax).Expression, beginning, out atTheBeginning);
                case SyntaxKind.SimpleMemberAccessExpression:
                    return MemberAccessChainExpressionStartsWith((memberAccessChain as MemberAccessExpressionSyntax).Expression, beginning, out atTheBeginning);
                case SyntaxKind.ElementAccessExpression:
                    return MemberAccessChainExpressionStartsWith((memberAccessChain as ElementAccessExpressionSyntax).Expression, beginning, out atTheBeginning);
                default:
                    atTheBeginning = null;
                    return false;
            }
        }
    }
}
