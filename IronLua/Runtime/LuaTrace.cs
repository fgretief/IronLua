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

            internal int variableStackLength = 0;
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

        private static readonly MethodInfo PushCallStackMethodInfo = typeof(LuaTrace).GetMethod("PushFunctionCall");
        private static readonly MethodInfo PopCallStackMethodInfo = typeof(LuaTrace).GetMethod("PopFunctionCall");

        public void PushFunctionCall(FunctionCall call)
        {
            if (call.FileName == null || call.FileName.Length == 0)
                call.FileName = CurrentDocument;
            call.variableStackLength = variableAccessStack.Count;
            CallStack.Push(call);
        }

        public void PopFunctionCall()
        {
            var call = CallStack.Pop();
            int expectedLength = call.variableStackLength;

            Stack<VariableAccess> globalSets = new Stack<VariableAccess>();
            while (variableAccessStack.Count > call.variableStackLength)
            {
                var access = variableAccessStack.Pop();
                if (access.Operation == AccessType.GlobalSet)
                    globalSets.Push(access);
            }

            while (globalSets.Count > 0)
                variableAccessStack.Push(globalSets.Pop());
        }

        public static Expression MakePushFunctionCall(LuaContext context, FunctionCall call)
        {
            return Expression.Call(Expression.Constant(context.Trace), PushCallStackMethodInfo, Expression.Constant(call));
        }

        public static Expression MakePopFunctionCall(LuaContext context)
        {
            return Expression.Call(Expression.Constant(context.Trace), PopCallStackMethodInfo);
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

        private Stack<VariableAccess> variableAccessStack = new Stack<VariableAccess>();

        public VariableAccess LastVariableAccess
        { get { return variableAccessStack.Peek(); } }

        public string CurrentVariableIdentifier
        {
            get
            {
                string identifier = null;
                foreach (var v in variableAccessStack)
                    switch (v.Operation)
                    {
                        case AccessType.GlobalGet:
                        case AccessType.GlobalSet:
                        case AccessType.LocalGet:
                        case AccessType.LocalSet:
                            identifier = v.VariableName + (identifier == null ? "" : ("." + identifier));
                            return identifier;

                        case AccessType.MemberGet:
                        case AccessType.MemberSet:
                            identifier = v.VariableName + (identifier == null ? "" : ("." + identifier));
                            break;
                            
                        case AccessType.IndexGet:
                        case AccessType.IndexSet:
                            identifier = "[" + v.VariableName + "]" + (identifier == null ? "" : ("." + identifier));
                            break;
                    }

                return "No current variable";
            }
        }

        public void UpdateLastVariableAccess(VariableAccess variable, object value)
        {
            variableAccessStack.Push(variable);
            LastVariableAccess.Value = value;
        }

        private static readonly MethodInfo UpdateLastVariableAccessMethodInfo = typeof(LuaTrace).GetMethod("UpdateLastVariableAccess");
        public static Expression MakeUpdateLastVariableAccess(LuaContext context, VariableAccess variable, Expression value)
        {
            return Expression.Call(Expression.Constant(context.Trace), UpdateLastVariableAccessMethodInfo, Expression.Constant(variable), value);
        }

        public IEnumerable<KeyValuePair<string,object>> AccessibleVariables
        {
            get
            {
                foreach (var v in variableAccessStack)
                    switch (v.Operation)
                    { 
                        case AccessType.GlobalSet:
                        case AccessType.LocalSet:
                            yield return new KeyValuePair<string, object>(v.VariableName, v.Value);
                            break;
                    }
            }
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
