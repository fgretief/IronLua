using System;
using System.Text;
using IronLua.Hosting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;
using System.Linq;

namespace IronLua.Tests.Features
{
    // ReSharper disable InconsistentNaming

    [TestFixture]
    public class LuaTests_Errors
    {
        ScriptEngine engine;

        [TestFixtureSetUp]
        public void PrepareEngine()
        {
            engine = Lua.CreateEngine();
        }

        public void PerformTest(string code, string expect)
        {
            string output, error;
            engine.ExecuteTestCode(code, out output, out error);

            Assert.That(output, Is.EqualTo(expect + Environment.NewLine));
            Assert.That(error, Is.Empty);
        }

        [Test, ExpectedException(ExpectedException = typeof(LuaErrorException), ExpectedMessage = "error message")]
        public void TestErrors_Throw()
        {
            engine.Execute("error('error message')");
        }

        [Test]
        public void TestErrors_PCall()
        {
            dynamic error = engine.Execute("a,x = pcall(error, 'error message'); return x");
            Assert.That((object)error != null);
            Assert.That((string)error == "error message");
        }

        [Test]
        public void TestErrors_XPCall()
        {
            string code = 
@"handler = function() x = 'error message' end
xpcall(function () error('error message2') end, handler)
return x";
            dynamic error = engine.Execute(code);
            Assert.That((object)error != null);
            Assert.That((string)error == "error message");
        }

        [Test]
        public void TestErrors_ChunkLevelCall()
        {
            string code = @"error('error message')";
            try
            {
                engine.Execute(code);
            }
            catch (LuaErrorException ex)
            {
                Assert.That(ex.StackLevel == 1);
                Assert.That(ex.Message == "(chunk):1:error message");
                Assert.That(ex.Result == "error message");

                return;
            }

            Assert.Fail();
        }

        [Test]
        public void TestErrors_FourLevelCall()
        {
            string code = 
@"function f1()
    error('error message')
end

function f2() f1() end
function f3() f2() end
function f4() f3() end

f4()
";
            try
            {
                engine.Execute(code);
            }
            catch (LuaErrorException ex)
            {
                Assert.That(ex.StackLevel == 1);
                Assert.That(ex.Message == "(chunk):2:error message");
                Assert.That(ex.Result as string == "error message");

                return;
            }

            Assert.Fail();
        }

//        [Test]
//        public void TestErrors_AccessibleVariables()
//        {
//            string code = 
//@"a = 10
//a = 11
//local b = 20
//
//function f1() 
//    c = 30 
//    local d = 40 
//    do 
//        local e = 50 
//    end 
//    error('Break') 
//end
//
//f1()";

//            try
//            {
//                engine.Execute(code);
//            }
//            catch (LuaErrorException ex)
//            {
//                Assert.That(ex.Result, Is.EqualTo("Break"));
//                Assert.That(ex.AccessibleVariables.Count() > 0);

//                //Check that the right variables are present
//                Assert.That(ex.AccessibleVariables.Any(x => x == "a"));
//                Assert.That(ex.AccessibleVariables.Any(x => x == "b"));
//                Assert.That(ex.AccessibleVariables.Any(x => x == "c"));
//                Assert.That(ex.AccessibleVariables.Any(x => x == "d"));
//                Assert.That(ex.AccessibleVariables.Any(x => x == "e"), Is.False);

//                //Check that their values are correct
//                Assert.That(ex.GetVariableValues("a").First(), Is.EqualTo(10));
//                Assert.That(ex.GetVariableValues("a").Last(), Is.EqualTo(11));
//                Assert.That(ex.GetVariableValues("b").First(), Is.EqualTo(20));
//                Assert.That(ex.GetVariableValues("c").First(), Is.EqualTo(30));
//                Assert.That(ex.GetVariableValues("d").First(), Is.EqualTo(40));
//            }
//        }
    }
}
