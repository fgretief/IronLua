using System.CodeDom;
using IronLua.Hosting;
using NUnit.Framework;

namespace IronLua.Tests.Runtime
{
    public class LuaCodeDomTests
    {
        [Test]
        public void Test1()
        {
            var compileUnit = new CodeCompileUnit();

            var samples = new CodeNamespace("Samples");

            samples.Imports.Add(new CodeNamespaceImport("System"));

            compileUnit.Namespaces.Add(samples);

            var class1 = new CodeTypeDeclaration("Class1");

            var start = new CodeEntryPointMethod()
            {
                Name = "Main",
                Attributes = MemberAttributes.Static
            };
            
            start.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string[]), "args"));

            var cs1 = new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression("System.Console"),
                "WriteLine", new CodePrimitiveExpression("Hello World!"));
            start.Statements.Add(cs1);

            class1.Members.Add(start);

            var lua = Lua.CreateEngine();

            var s = lua.CreateScriptSource(start);
            
            TestContext.WriteLine(s.GetCode());
        }
    }
}
