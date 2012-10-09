using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting;

namespace IronLua.Runtime
{
    public class TraceBack
    {
        private readonly TraceBack _next;
        private readonly TraceBackFrame _frame;
        private int _line;

        public TraceBack(TraceBack nextTraceBack, TraceBackFrame fromFrame) {
            _next = nextTraceBack;
            _frame = fromFrame;
        }

        public TraceBack Next {
            get {
                return _next;
            }
        }

        public TraceBackFrame Frame {
            get {
                return _frame;
            }
        }

        public int LineNumber {
            get {
                return _line;
            }
        }
        
        internal void SetLine(int lineNumber)
        {
            _line = lineNumber;
        }
    }

    public class TraceBackFrame
    {
        private readonly LuaTracebackListener _traceAdapter;
        private TracebackDelegate _trace;
        private object _traceObject;
        internal int _lineNo;
        private readonly LuaDebuggingPayload _debugProperties;
        private readonly Func<IDictionary<object, object>> _scopeCallback;

        private readonly LuaTable _globals;
        private readonly object _locals;
        private readonly FunctionCode _code;
        private readonly CodeContext/*!*/ _context;
        private readonly TraceBackFrame _back;

        internal TraceBackFrame(CodeContext/*!*/ context, LuaTable globals, object locals, FunctionCode code)
        {
            _globals = globals;
            _locals = locals;
            _code = code;
            _context = context;
        }

        internal TraceBackFrame(CodeContext/*!*/ context, LuaTable globals, object locals, FunctionCode code, TraceBackFrame back)
        {
            _globals = globals;
            _locals = locals;
            _code = code;
            _context = context;
            _back = back;
        }

        internal TraceBackFrame(LuaTracebackListener traceAdapter, FunctionCode code, TraceBackFrame back, LuaDebuggingPayload debugProperties, Func<IDictionary<object, object>> scopeCallback)
        {
            _traceAdapter = traceAdapter;
            _code = code;
            _back = back;
            _debugProperties = debugProperties;
            _scopeCallback = scopeCallback;
        }

        [SpecialName, PropertyMethod]
        public object GetTrace()
        {
            if (_traceAdapter != null)
            {
                return _traceObject;
            }
            else
            {
                return null;
            }
        }

        [SpecialName, PropertyMethod]
        public void SetTrace(object value)
        {
            _traceObject = value;
            _trace = (TracebackDelegate)ConvertToDelegate(value, typeof(TracebackDelegate));
        }

        [SpecialName, PropertyMethod]
        public void DeleteTrace()
        {
            SetTrace(null);
        }
        
        object ConvertToDelegate(object value, Type to)
        {
            if (value == null) return null;
            return _context.Language.DelegateCreator.GetDelegate(value, to);
        }

        internal CodeContext Context
        {
            get
            {
                return _context;
            }
        }

        internal TracebackDelegate TraceDelegate
        {
            get
            {
                if (_traceAdapter != null)
                {
                    return _trace;
                }
                else
                {
                    return null;
                }
            }
        }

        public LuaTable Globals
        {
            get
            {
                object context;
                if (_scopeCallback != null &&
                    _scopeCallback().TryGetValue("$globalContext", out context) && context != null)
                {
                    return ((CodeContext)context).EngineGlobals.Storage;
                }
                else
                {
                    return _globals;
                }
            }
        }

        public TraceBackFrame Back
        {
            get
            {
                return _back;
            }
        }

        public object LineNumber
        {
            get
            {
                if (_traceAdapter != null)
                {
                    return _lineNo;
                }
                else
                {
                    return 1;
                }
            }
            set
            {
                double temp = 0;
                if (!double.TryParse(value.ToString(),out temp))
                {
                    throw LuaRuntimeException.Create(_context, "LineNumber must be a number");
                }

                int newLineNum = (int)temp;

                if (_traceAdapter != null)
                {
                    SetLineNumber(newLineNum);
                }
                else
                {
                    throw LuaRuntimeException.Create(_context, "LineNumber can only be set by a trace function");
                }
            }
        }

        private void SetLineNumber(int newLineNum)
        {
            var pyThread = PythonOps.GetFunctionStackNoCreate();
            if (!IsTopMostFrame(pyThread))
            {
                if (!TracingThisFrame(pyThread))
                {
                    throw LuaRuntimeException.Create(Context, "LineNumber can only be set by a trace function");
                }
                else                
                    return;                
            }

            FunctionCode funcCode = _debugProperties.Code;
            Dictionary<int, List<int>> loopLocations = _debugProperties.LoopLocations;
            Dictionary<int, bool> handlerLocations = _debugProperties.HandlerLocations;

            List<int> currentLoopIds = null;
            bool inForLoopOrFinally = loopLocations != null && loopLocations.TryGetValue(_lineNo, out currentLoopIds);

            int originalNewLine = newLineNum;

            if (newLineNum < funcCode.Span.Start.Line)
            {
                throw LuaRuntimeException.Create(Context, "line {0} comes before the current code block", newLineNum);
            }
            else if (newLineNum > funcCode.Span.End.Line)
            {
                throw LuaRuntimeException.Create(Context, "line {0} comes after the current code block", newLineNum);
            }


            while (newLineNum <= funcCode.Span.End.Line)
            {
                var span = new SourceSpan(new SourceLocation(0, newLineNum, 1), new SourceLocation(0, newLineNum, Int32.MaxValue));

                // Check if we're jumping onto a handler
                bool handlerIsFinally;
                if (handlerLocations != null && handlerLocations.TryGetValue(newLineNum, out handlerIsFinally))
                {
                    throw LuaRuntimeException.Create(Context, "can't jump to 'except' line");
                }

                if (_traceAdapter.LuaContext.TracePipeline.CanSetNextStatement((string)((FunctionCode)_code).FileName, span))
                {
                    _traceAdapter.LuaContext.TracePipeline.SetNextStatement((string)((FunctionCode)_code).FileName, span);
                    _lineNo = newLineNum;
                    return;
                }

                ++newLineNum;
            }

            throw LuaRuntimeException.Create(Context, "line {0} is invalid jump location ({1} - {2} are valid)", originalNewLine, funcCode.Span.Start.Line, funcCode.Span.End.Line);
        }

        private bool TracingThisFrame(List<FunctionStack> luaThread)
        {
            return luaThread != null &&
                luaThread.FindIndex(x => x.Frame == this) != -1;
        }

        private bool IsTopMostFrame(List<FunctionStack> luaThread)
        {
            return luaThread != null && luaThread.Count != 0 && Type.ReferenceEquals(this, luaThread[luaThread.Count - 1].Frame);
        }

        private static Exception BadForJump(CodeContext context, int newLineNum, Dictionary<int, bool> jumpIntoLoopIds)
        {
            return LuaRuntimeException.Create(context, "can't jump into loops", newLineNum);
        }
    }

    public delegate TracebackDelegate TracebackDelegate(TraceBackFrame frame, string result, object payload);
}
