using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace VSDiagnostics.Diagnostics.Exceptions.ArgumentExceptionWithoutNameofOperator
{
    [ExportCodeFixProvider(nameof(ArgumentExceptionWithoutNameofOperatorCodeFix), LanguageNames.CSharp), Shared]
    public class ArgumentExceptionWithoutNameofOperatorCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ArgumentExceptionWithoutNameofOperatorAnalyzer.Rule.Id);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var objectCreationExpression = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(VSDiagnosticsResources.ArgumentExceptionWithoutNameofOperatorCodeFixTitle, x => UseNameofAsync(context.Document, objectCreationExpression),
                    nameof(ArgumentExceptionWithoutNameofOperatorAnalyzer)), diagnostic);
        }

        private async Task<Document> UseNameofAsync(Document document, ObjectCreationExpressionSyntax objectCreationExpression)
        {
            var method = objectCreationExpression.Ancestors().OfType<MethodDeclarationSyntax>().First();
            var methodParameters = method.ParameterList.Parameters;
            var expressionArguments = objectCreationExpression.ArgumentList.Arguments.Select(x => x.Expression).OfType<LiteralExpressionSyntax>();

            var editor = await DocumentEditor.CreateAsync(document);

            foreach (var expressionArgument in expressionArguments)
            {
                foreach (var methodParameter in methodParameters)
                {
                    if (string.Equals(methodParameter.Identifier.ValueText, expressionArgument.Token.ValueText, StringComparison.OrdinalIgnoreCase))
                    {
                        var newExpression = SyntaxFactory.ParseExpression($"nameof({methodParameter.Identifier})");
                        editor.ReplaceNode(expressionArgument, newExpression);
                        return editor.GetChangedDocument();
                    }
                }
            }

            throw new InvalidOperationException(nameof(ArgumentExceptionWithoutNameofOperatorCodeFix));
        }
    }
}