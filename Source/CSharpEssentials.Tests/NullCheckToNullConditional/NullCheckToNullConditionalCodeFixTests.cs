using RoslynNUnitLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis;
using CSharpEssentials.NullCheckToNullConditional;
using NUnit.Framework;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpEssentials.Tests.NullCheckToNullConditional
{
    class NullCheckToNullConditionalCodeFixTests : CodeFixTestFixture
    {
        protected override string LanguageName => LanguageNames.CSharp;
        protected override CodeFixProvider CreateProvider() => new NullCheckToNullConditionalCodeFix();

        const string codeBase = @"
class SuperAwesomeCode
{
    interface A { B b(); }
    interface B { C c { get; } }
    interface C { DHolder this[int i] { get; } }
    interface DHolder { D d { get; } }
    interface D { void m(object o1, object o2); MyStruct? myStruct{ get; } }
    struct MyStruct { int this[int i] => i; }
    void M(A a, B b, C c, DHolder dHolder, D d, object blah, dynamic dyn)
    {
        <<<<<code>>>>>
    }
}
";
        string InsertCode(string s) => codeBase.Replace("<<<<<code>>>>>", s);

        [Test]
        public void SimpleTest()
        {
            var markupCode = InsertCode("[|if (null != d )\r\n            d.m(blah, blah);|]");
            var expected = InsertCode("d?.m(blah, blah);");
            TestCodeFix(markupCode, expected, DiagnosticDescriptors.UseNullConditionalMemberAccess);
        }

        [Test]
        public void TestPropertyAccessor()
        {
            var markupCode = InsertCode("[|if (b != null)  b.c.ToString();|]");
            var expected = InsertCode("b?.c.ToString();");
            TestCodeFix(markupCode, expected, DiagnosticDescriptors.UseNullConditionalMemberAccess);
        }

        [Test]
        public void TestIndexer()
        {
            var markupCode = InsertCode("[|if (b.c != null)  b.c[0].ToString();|]");
            var expected = InsertCode("b.c?[0].ToString();");
            TestCodeFix(markupCode, expected, DiagnosticDescriptors.UseNullConditionalMemberAccess);
        }

        [Test]
        public void TestNullableValueType()
        {
            var markupCode = InsertCode("[|if (d.myStruct != null)  d.myStruct.Value[0].CompareTo(42).ToString();|]");
            var expected = InsertCode("d.myStruct?[0].CompareTo(42).ToString();");
            TestCodeFix(markupCode, expected, DiagnosticDescriptors.UseNullConditionalMemberAccess);
        }

        [Test]
        public void TestDynamicExpression()
        {
            var markupCode = InsertCode("[|if (dyn.x.y.z != null) dyn.x.y.z.m();|]");
            var expected = InsertCode("dyn.x.y.z?.m();");
            TestCodeFix(markupCode, expected, DiagnosticDescriptors.UseNullConditionalMemberAccess);
        }

        [Test]
        public void TestBlockStatement()
        {
            var markupCode = InsertCode("[|if (a.b() != null) { a.b().c[0].ToString(); }|]");
            var expected = InsertCode("a.b()?.c[0].ToString();");
            TestCodeFix(markupCode, expected, DiagnosticDescriptors.UseNullConditionalMemberAccess);
        }

        [Test]
        public void TestTriviaIsPreserved()
        {
            var markupCode = 
@"class Trivial{
    void F(object o){
        [|//comment1
        if (o != null) {
            //comment2
            o.ToString();
        }|]
    }
}";
            var expected =
@"class Trivial{
    void F(object o){
        //comment1
        //comment2
        o?.ToString();

    }
}";

            TestCodeFix(markupCode, expected, DiagnosticDescriptors.UseNullConditionalMemberAccess);
        }

        [Test]
        public void TestInvocationStartsWith()
        {
            var code = InsertCode("[|if (a != null)  a.b().c[1].d.m(blah, blah);|]");
            var expeced = InsertCode("a?.b().c[1].d.m(blah, blah);");

            Document doc;
            TextSpan span;
            TestHelpers.TryGetDocumentAndSpanFromMarkup(code, LanguageNames.CSharp, out doc, out span);
            var root = doc.GetSyntaxRootAsync().Result;
            var ifStatement = root.FindNode(span) as IfStatementSyntax;
            var exp = (ifStatement.Condition as BinaryExpressionSyntax).Left;
            var chain = (ifStatement.Statement as ExpressionStatementSyntax).Expression;
            ExpressionSyntax _;
            Assert.True(NullCheckToNullConditionalCodeFix.MemberAccessChainExpressionStartsWith(chain, exp, out _));

        }
    }
}
