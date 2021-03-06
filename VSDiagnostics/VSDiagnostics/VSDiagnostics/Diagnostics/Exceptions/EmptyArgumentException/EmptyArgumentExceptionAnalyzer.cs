﻿using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using VSDiagnostics.Utilities;

namespace VSDiagnostics.Diagnostics.Exceptions.EmptyArgumentException
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EmptyArgumentExceptionAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = nameof(EmptyArgumentExceptionAnalyzer);
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private static readonly string Category = VSDiagnosticsResources.ExceptionsCategory;
        private static readonly string Message = VSDiagnosticsResources.EmptyArgumentExceptionAnalyzerMessage;
        private static readonly string Title = VSDiagnosticsResources.EmptyArgumentExceptionAnalyzerTitle;

        internal static DiagnosticDescriptor Rule => new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.ThrowStatement);
        }

        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            var throwStatement = context.Node as ThrowStatementSyntax;

            var expression = throwStatement?.Expression as ObjectCreationExpressionSyntax;
            if (expression == null)
            {
                return;
            }

            var symbolInformation = context.SemanticModel.GetSymbolInfo(expression.Type);
            if (!symbolInformation.Symbol.InheritsFrom(typeof (ArgumentException)))
            {
                return;
            }

            if (!expression.ArgumentList.ChildNodes().Any())
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation()));
            }
        }
    }
}