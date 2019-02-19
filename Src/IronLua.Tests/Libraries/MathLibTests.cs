using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.Scripting.Hosting;
using IronLua.Hosting;

namespace IronLua.Tests.Libraries
{
    [TestFixture]
    class MathLibTests
    {
        ScriptEngine engine;

        [OneTimeSetUp]
        public void PrepareEngine()
        {
            engine = Lua.CreateEngine();
        }

        [Test]
        public void MathTests_Sin()
        {
            string code =
@"
local function near(val,expected)
    return math.abs(val - expected) < 0.00000000001
end

local f = math.sin
assert(f,'Function not defined')
local p = { [0] = 0, [math.pi / 2] = 1, [math.pi] = 0, [3 * math.pi / 2] = -1, [2 * math.pi] = 0 }

for arg,expected in pairs(p) do assert(near(f(arg),expected)) end
";
            engine.Execute(code);
        }


        [Test]
        public void MathTests_Cos()
        {
            string code =
@"
local function near(val,expected)
    return math.abs(val - expected) < 0.00000000001
end

local f = math.cos
assert(f,'Function not defined')
local p = { [0] = 1, [math.pi / 2] = 0, [math.pi] = -1, [3 * math.pi / 2] = 0, [2 * math.pi] = 1 }

for arg,expected in pairs(p) do assert(near(f(arg),expected)) end
";
            engine.Execute(code);
        }

        [Test]
        public void MathTests_Random()
        {
            string code =
@"
local rand = math.random
local srand = math.randomseed

local function between(value,low,high) return value < high and low < value end

srand(10)
assert(between(rand(),0,1))
assert(between(rand(6),1,6))
";

            engine.Execute(code);
        }
    }
}
