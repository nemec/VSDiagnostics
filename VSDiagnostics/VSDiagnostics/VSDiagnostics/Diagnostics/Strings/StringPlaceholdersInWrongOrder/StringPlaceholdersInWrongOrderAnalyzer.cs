﻿using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VSDiagnostics.Diagnostics.Strings.StringPlaceholdersInWrongOrder
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StringPlaceholdersInWrongOrderAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string Category = VSDiagnosticsResources.StringsCategory;
        private const string DiagnosticId = nameof(StringPlaceholdersInWrongOrderAnalyzer);
        private static readonly string Message = VSDiagnosticsResources.StringPlaceholdersInWrongOrderMessage;
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;
        private static readonly string Title = VSDiagnosticsResources.StringPlaceholdersInWrongOrderTitle;

        internal static DiagnosticDescriptor Rule => new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocation = context.Node as InvocationExpressionSyntax;
            if (invocation == null)
            {
                return;
            }

            // Verify we're dealing with a string.Format() call
            var memberAccessExpression = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpression != null)
            {
                var invokedType = context.SemanticModel.GetSymbolInfo(memberAccessExpression.Expression);
                var invokedMethod = context.SemanticModel.GetSymbolInfo(memberAccessExpression.Name);
                if (invokedType.Symbol == null || invokedMethod.Symbol == null)
                {
                    return;
                }

                if (invokedType.Symbol.MetadataName != typeof(string).Name || invokedMethod.Symbol.MetadataName != nameof(string.Format))
                {
                    return;
                }
            }

            // Verify the format is a literal expression and not a method invocation or an identifier
            // The overloads are in the form string.Format(string, object[]) or string.Format(CultureInfo, string, object[])
            if (invocation.ArgumentList == null || invocation.ArgumentList.Arguments.Count < 2)
            {
                return;
            }

            var firstArgument = invocation.ArgumentList.Arguments[0];
            var secondArgument = invocation.ArgumentList.Arguments[1];

            var firstArgumentSymbol = context.SemanticModel.GetSymbolInfo(firstArgument.Expression);
            if (!(firstArgument.Expression is LiteralExpressionSyntax) &&
                (firstArgumentSymbol.Symbol?.MetadataName == typeof(CultureInfo).Name && !(secondArgument?.Expression is LiteralExpressionSyntax)))
            {
                return;
            }

            // Get the formatted string from the correct position
            var firstArgumentIsLiteral = firstArgument.Expression is LiteralExpressionSyntax;
            var formatString = firstArgumentIsLiteral
                ? ((LiteralExpressionSyntax) firstArgument.Expression).GetText().ToString()
                : ((LiteralExpressionSyntax) secondArgument.Expression).GetText().ToString();

            // Verify that all placeholders are counting from low to high.
            // Not all placeholders have to be used necessarily, we only re-order the ones that are actually used in the format string.
            //
            // Display a warning when the integers in question are not in ascending or equal order. 
            var placeholders = StringPlaceholdersInWrongOrderHelper.GetPlaceholders(formatString);

            // If there's no placeholder used or there's only one, there's nothing to re-order
            if (placeholders.Count <= 1)
            {
                return;
            }

            for (var index = 1; index < placeholders.Count; index++)
            {
                int firstValue, secondValue;
                if (!int.TryParse(StringPlaceholdersInWrongOrderHelper.Normalize(placeholders[index - 1].Value), out firstValue) ||
                    !int.TryParse(StringPlaceholdersInWrongOrderHelper.Normalize(placeholders[index].Value), out secondValue))
                {
                    // Parsing failed
                    return;
                }

                // Given a scenario like this:
                //     string.Format("{0} {1} {4} {3}", a, b, c, d)
                // it would otherwise crash because it's trying to access index 4, which we obviously don't have.
                var argumentsToSkip = firstArgumentIsLiteral ? 1 : 2;
                if (firstValue >= invocation.ArgumentList.Arguments.Count - argumentsToSkip || secondValue >= invocation.ArgumentList.Arguments.Count - argumentsToSkip)
                {
                    return;
                }

                // They should be in ascending or equal order
                if (firstValue > secondValue)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
                    return;
                }
            }
        }
    }
}