using System;
using IronLua.Hosting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;

namespace IronLua.Tests.Features
{
    // ReSharper disable InconsistentNaming

    [TestFixture]
    public class LuaTests_type
    {
        ScriptEngine engine;

        [OneTimeSetUp]
        public void PrepareEngine()
        {
            engine = Lua.CreateEngine();
        }

        public void PerformTest(string code, string expect)
        {
            dynamic result = engine.ExecuteTestCode(code, out var output, out var error);
            Assert.That((object)result, Is.Null);
            Assert.That(output, Is.EqualTo(expect + Environment.NewLine));
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void TestType_number()
        {
            string code = @"print(type(0))";

            PerformTest(code, "number");
        }

        [Test]
        public void TestType_string()
        {
            string code = @"print(type('0'))";

            PerformTest(code, "string");
        }

        [Test]
        public void TestType_table()
        {
            string code = @"print(type({}))";

            PerformTest(code, "table");
        }

        [Test]
        public void TestType_nil()
        {
            string code = @"print(type(nil))";

            PerformTest(code, "nil");
        }

        [Test]
        public void TestType_VariableXyzThatDontExist()
        {
            string code = @"print(type(Xyz))";

            PerformTest(code, "nil");
        }
    }
}
