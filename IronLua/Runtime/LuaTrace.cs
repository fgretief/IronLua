using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Scripting;
using System.Linq.Expressions;
using System.Reflection;
using System.Dynamic;
using IronLua.Compiler;

namespace IronLua.Runtime
{
    class LuaTrace
    {
        LuaContext _context;

        public LuaTrace(LuaContext context)
        {
            _context = context;
            CallStack = new Stack<FunctionCall>();
        }

        #region Current File

        public string CurrentDocument
        { get; private set; }

        public void UpdateCurrentDocument(SymbolDocumentInfo document)
        {
            CurrentDocument = document == null ? "(chunk)" : document.FileName;
        }

        private static readonly MethodInfo UpdateCurrentDocumentMethodInfo = typeof(LuaTrace).GetMethod("UpdateCurrentDocument");
        public static Expression MakeUpdateCurrentDocument(LuaContext context, SymbolDocumentInfo document)
        {
            return Expression.Call(Expression.Constant(context.Trace), UpdateCurrentDocumentMethodInfo, Expression.Constant(document));
        }

        #endregion

        #region Current Source Span

        public SourceSpan CurrentSpan
        { get; private set; }

        public void UpdateSourceSpan(SourceSpan span)
        {
            CurrentSpan = span;
        }

        private static readonly MethodInfo UpdateSourceSpanMethodInfo = typeof(LuaTrace).GetMethod("UpdateSourceSpan");
        public static Expression MakeUpdateSourceSpan(LuaContext context, SourceSpan span)
        {
            return Expression.Call(Expression.Constant(context.Trace), UpdateSourceSpanMethodInfo, Expression.Constant(span));
        }

        #endregion

        #region Function Call Stack

        public class FunctionCall
        {
            public FunctionCall(SourceSpan functionLocation, FunctionType type, string identifier, string fileName = null)
            {
                FunctionLocation = functionLocation;
                Type = type;
                FileName = fileName;
                MethodName = identifier;
            }

            public FunctionCall(SourceSpan functionLocation, FunctionType type, IEnumerable<string> identifiers, string fileName = null)
            {
                FunctionLocation = functionLocation;
                Type = type;
                FileName = fileName;

                string temp = identifiers.First();
                foreach (var i in identifiers.Skip(1))
                    temp += "." + i;

                MethodName = temp;
            }

            public string FileName { get; internal set; }
            public string MethodName { get; private set; }
            public SourceSpan FunctionLocation { get; private set; }
            public FunctionType Type { get; private set; }
        }

        public enum FunctionType
        {
            Lua,
            CLR,
            Chunk,
            Invoke
        }

        public Stack<FunctionCall> CallStack
        { get; private set; }

        private static readonly MethodInfo PushCallStackMethodInfo = typeof(Stack<FunctionCall>).GetMethod("Push");
        private static readonly MethodInfo PopCallStackMethodInfo = typeof(Stack<FunctionCall>).GetMethod("Pop");

        public static Expression MakePushFunctionCall(LuaContext context, FunctionCall call)
        {
            if (call.FileName == null)
                call.FileName = context.Trace.CurrentDocument;
            return Expression.Call(Expression.Constant(context.Trace.CallStack), PushCallStackMethodInfo, Expression.Constant(call));
        }

        public static Expression MakePopFunctionCall(LuaContext context)
        {
            return Expression.Call(Expression.Constant(context.Trace.CallStack), PopCallStackMethodInfo);
        }

        #endregion

        #region Variable Access Stack

        public enum AccessType
        {
            GlobalGet, GlobalSet,
            LocalGet, LocalSet,
            MemberGet, MemberSet,
            IndexGet, IndexSet
        }

        public class VariableAccess
        {
            public VariableAccess(string identifier, AccessType operation)
            {
                VariableName = identifier;
                Operation = operation;
            }

            public string VariableName
            { get; private set; }
            
            public AccessType Operation
            { get; private set; }

            public object Value
            { get; internal set; }
        }

        public VariableAccess LastVariableAccess
        { get; private set; }

        public void UpdateLastVariableAccess(VariableAccess variable, object value)
        {
            LastVariableAccess = variable;
            LastVariableAccess.Value = value;
        }

        private static readonly MethodInfo UpdateLastVariableAccessMethodInfo = typeof(LuaTrace).GetMethod("UpdateLastVariableAccess");
        public static Expression MakeUpdateLastVariableAccess(LuaContext context, VariableAccess variable, Expression value)
        {
            return Expression.Call(Expression.Constant(context.Trace), UpdateLastVariableAccessMethodInfo, Expression.Constant(variable), value);
        }
        #endregion

        #region Current Scope

        public IDynamicMetaObjectProvider CurrentScopeStorage
        { get; private set; }

        public void UpdateCurrentScopeStorage(IDynamicMetaObjectProvider scopeStorage)
        {
            CurrentScopeStorage = scopeStorage;
        }

        public LuaScope CurrentEvaluationScope
        { get; private set; }

        public void UpdateCurrentEvaluationScope(LuaScope scope)
        {
            CurrentEvaluationScope = scope;
        }


        private static readonly MethodInfo UpdateCurrentEvaluationScopeMethodInfo = typeof(LuaTrace).GetMethod("UpdateCurrentEvaluationScope");
        private static readonly MethodInfo UpdateScopeStorageMethodInfo = typeof(LuaTrace).GetMethod("UpdateCurrentScopeStorage");
        public static Expression MakeUpdateCurrentEvaluationScope(LuaContext context, LuaScope scope)
        {
            return Expression.Block(
                Expression.Call(Expression.Constant(context.Trace), UpdateCurrentEvaluationScopeMethodInfo, Expression.Constant(scope)),
                Expression.Call(Expression.Constant(context.Trace), UpdateScopeStorageMethodInfo, scope.GetDlrGlobals()));
        }

        #endregion
    }
}
