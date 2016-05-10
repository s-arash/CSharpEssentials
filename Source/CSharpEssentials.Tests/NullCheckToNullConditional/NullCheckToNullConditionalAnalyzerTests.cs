using RoslynNUnitLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using CSharpEssentials.NullCheckToNullConditional;
using NUnit.Framework;

namespace CSharpEssentials.Tests.NullCheckToNullConditional
{
    class NullCheckToNullConditionalAnalyzerTests : AnalyzerTestFixture
    {
        protected override string LanguageName => LanguageNames.CSharp;

        protected override DiagnosticAnalyzer CreateAnalyzer() => new NullCheckToNullConditionalAnalyzer();

        [Test]
        public void TestNoFixOnComipleError()
        {
            const string markup = @"
class C
{
    void M(object o)
    {
        if(o.GetType != null){
            o.GetType.ToString()
        }
    }
}
";
            NoDiagnostic(markup, DiagnosticIds.UseNullConditional);
        }

        [Test]
        public void TestNoFixOnNoneInvocationBody()
        {
            const string markup = @"
class C
{
    object M(object o)
    {
        if(o != null){
            return o;
        }
    }
}
";
            NoDiagnostic(markup, DiagnosticIds.UseNullConditional);
        }
    }
}
