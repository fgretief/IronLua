using System;
using IronLua.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;

namespace IronLua.Tests.Features
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable ConvertToConstant.Local

    [TestFixture]
    public class LuaTests_FunctionBehaviour
    {
        ScriptEngine engine;

        [OneTimeSetUp]
        public void PrepareEngine()
        {
            engine = Lua.CreateEngine();
        }

        public void PerformTest(string code, string expect)
        {
            string output, error;
            dynamic result = engine.CaptureOutput(e => e.Execute(code), out output, out error);

            Assert.That((object)result, Is.Null);
            Assert.That(output, Is.EqualTo(expect + Environment.NewLine));
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void FunctionBehaviour_RecursiveMissing()
        {
            string code =
                @"
local fact = function (n)
      if n == 0 then return 1
      else return n*fact(n-1)   -- buggy
      end
    end

return fact(10)
";

            Assert.Throws<LuaRuntimeException>(() =>
            {
                var result = engine.Execute(code);
                Assert.That(result, Is.EqualTo(3628800));
            }).WithMessage("could not find the variable 'fact'");
        }

        [Test]
        public void FunctionBehaviour_RecursiveLambda()
        {
            string code =
@"
local fact
    fact = function (n)
      if n == 0 then return 1
      else return n*fact(n-1)
      end
    end

return fact(10);
";

            var result = engine.Execute(code);
            Assert.That(result, Is.EqualTo(3628800));
        }


        [Test]
        public void FunctionBehaviour_RecursiveLocal()
        {
            string code =
@"
local function fact (n)
      if n == 0 then return 1
      else return n*fact(n-1)
      end
    end

return fact(10);
";

            var result = engine.Execute(code);
            Assert.That(result, Is.EqualTo(3628800));
        }
    }
}

#if false
/// Python code & AST for fractoral function

>>>def factoral(n):
...   if n < 2:
...     return 1
...   else:
...     return n * fractoral(n - 1)
...
//
// AST <undefined>
//

.codeblock Object <undefined> ( global,)() {
    .var Object fractoral (Local)

    {
        /*empty*/;
        (Void)(.bound fractoral) = (PythonOps.MakeFunction)(
            .context,
            "fractoral",
            .block (fractoral #1),
            .new String[] = {
                "n",
            },
            .new Object[] = {},
            (FunctionAttributes)None,
            .null,
            1,
            .null,
        );
    }
}
//
// CODE BLOCK: fractoral (1)
//

.codeblock Object fractoral ()(
    .arg Object n (Parameter,InParameterArray)
) {

    {
        .if (.action (Boolean) Do LessThan( // DoOperation LessThan
            (.bound n)
            2
        ) ) {{
            .return (Object)1;
        }
        } .else {{
            .return .action (Object) Do Multiply( // DoOperation Multiply
                (.bound n)
                .action (Object) Call( // CallSimple
                    (.bound fractoral)
                    .action (Object) Do Subtract( // DoOperation Subtract
                        (.bound n)
                        1
                    )
                )
            );
        }
        };
        /*empty*/;
    }
}
>>> print( fractoral(6) )
//
// AST <undefined>
//

.codeblock Object <undefined> ( global,)() {
    .var Object fractoral (Local)

    {
        /*empty*/;
        {
            (PythonOps.Print)(
                .action (Object) Call( // CallSimple
                    (.bound fractoral)
                    6
                ),
            );
        }
    }
}
720
>>>
#endif
