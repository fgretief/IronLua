using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronLua.Runtime;

namespace IronLua.Library
{
    class DebugLibrary : Library
    {
        public DebugLibrary(CodeContext context)
            : base(context)
        { }
        
        public override void Setup(IDictionary<string, object> table)
        {
            table.AddOrSet("getlocal", (Func<object, object, Varargs>)GetLocal);
            table.AddOrSet("setlocal", (Func<object, object, object, object>)SetLocal);
        }

        private Varargs GetLocal(object stackLevel, object varIndex)
        {
            var access = Context.GetLocalVariables(Convert.ToInt32(stackLevel) - 1);
            var index = Convert.ToInt32(varIndex);

            if(index > access.Count || index < 0)
                return null;

            var name = Context.GetLocalVariableName(Convert.ToInt32(stackLevel) - 1, index - 1);

            return new Varargs(name, access[index - 1]);
        }

        private object SetLocal(object stackLevel, object varIndex, object value)
        {
            var access = Context.GetLocalVariables(Convert.ToInt32(stackLevel) - 1);
            var index = Convert.ToInt32(varIndex);

            if (index > access.Count || index < 0)
                return null;

            var name = Context.GetLocalVariableName(Convert.ToInt32(stackLevel) - 1, index - 1);

            access[index - 1] = value;

            return name;
        }

    }
}
