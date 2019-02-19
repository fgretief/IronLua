using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.Scripting.Hosting;
using IronLua.Hosting;
using System.Diagnostics;
using IronLua.Runtime;

namespace IronLua.Tests.Features
{

    [TestFixture]
    public class LuaTests_Advanced
    {
        ScriptEngine engine;

        [OneTimeSetUp]
        public void PrepareEngine()
        {
            engine = Lua.CreateEngine();
        }

        public ScriptScope PerformTest(string code, StringBuilder expect)
        {
            var scope = engine.CreateScope();

            string output, error;
            dynamic result = engine.ExecuteTestCode(code, scope, out output, out error);

            Assert.That((object)result, Is.Null);
            Assert.That(output, Is.EqualTo(expect.ToString()));
            Assert.That(error, Is.Empty);

            return scope;
        }

        public ScriptScope PerformVariableTest(string code, params string[] varNames)
        {
            var scope = engine.CreateScope();

            string output, error;
            dynamic result = engine.ExecuteTestCode(code, scope, out output, out error);

            Assert.That((object)result, Is.Null);
            Assert.That(output, Is.Empty);
            Assert.That(error, Is.Empty);

            foreach (var varName in varNames)
            {
                Assert.That(scope.ContainsVariable(varName), Is.True, "Variable '" + varName + "' is missing!");
            }
            return scope;
        }

        
        [Test]
        public void TestAdvanced_Closures()
        {
            string code = 
@"function newCounter()
    local i = 0
    return function () i = i + 1 return i end
end

c1 = newCounter()
c2 = newCounter()

assert(c1() == 1)
assert(c2() == 1)
assert(c1() == 2)";

            engine.Execute(code);
        }

        [Test]
        public void TestAdvanced_FunctionEnvironments()
        {
            string code =
@"
a = 2
function f1() assert(a == 10) end

env = { a = 10, assert = assert }
setfenv(f1,env)
f1()
";
            engine.Execute(code);
        }

        [Test]
        public void TestAdvanced_RedefiningFunctions()
        {
            string code = 
@"
assert(math, 'Failed to require math library')
local oldSin = math.sin
assert(oldSin,'Failed to get math.sin function')
math.sin = function(degrees) return oldSin(degrees * math.pi / 180) end

assert(math.sin(90) == 1)
";
            engine.Execute(code);
        }

    }
}
