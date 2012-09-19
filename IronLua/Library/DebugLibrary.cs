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
        }

        private Varargs GetLocal(object stackLevel, object varIndex)
        {
            var access = Context.GetVariableAccess(Convert.ToInt32(stackLevel), Convert.ToInt32(varIndex));
            return new Varargs(access.VariableName, access.Value);
        }

    }
}
