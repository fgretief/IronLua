using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



using Debugging = Microsoft.Scripting.Debugging;
using System.Diagnostics;

namespace IronLua.Runtime
{
    internal class LuaTracebackListener : Debugging.ITraceCallback
    {
        private readonly LuaContext _luaContext;
        [ThreadStatic]
        private static TracebackDelegate _globalTraceDispatch;
        [ThreadStatic]
        private static object _globalTraceObject;
        [ThreadStatic]
        internal static bool InTraceBack;
        private bool _exceptionThrown;

        public LuaContext LuaContext
        { get { return _luaContext; } }

        public LuaTracebackListener(LuaContext context)
        {
            _luaContext = context;
        }

        public void OnTraceEvent(Debugging.TraceEventKind kind, string name, string sourceFileName, Microsoft.Scripting.SourceSpan sourceSpan, Func<IDictionary<object, object>> scopeCallback, object payload, object customPayload)
        {
            throw new NotImplementedException();
        }
    }
}
