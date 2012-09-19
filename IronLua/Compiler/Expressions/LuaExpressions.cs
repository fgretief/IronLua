using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Expr = System.Linq.Expressions.Expression;
using IronLua.Runtime;
using System.Linq.Expressions;
using Microsoft.Scripting;

namespace IronLua.Compiler.Expressions
{
    static class LuaExpressions
    {
        /// <summary>
        /// Creates a wrapper around a function which represents entry into its scope
        /// </summary>
        /// <param name="context">The context under which this function is compiled</param>
        /// <param name="body">The block expression which represents this function's execution</param>
        /// <param name="evalScope">The scope in which the body will be executed, holding local values</param>
        /// <param name="upScope">The scope in which the function will execute, storing up values (which persist between calls)</param>
        public static Expr FunctionScope(CodeContext context, LuaScope evalScope, LuaScope upScope, IEnumerable<string> identifiers, Expr body)
        {
            return new FunctionScopeExpression(context, evalScope, upScope, identifiers, body);
        }
        
        /// <summary>
        /// Wraps an expression in <see cref="DebugInfoExpression"/>s which indicate the <see cref="SouceSpan"/> where it is defined
        /// </summary>
        public static Expr SourceSpan(SymbolDocumentInfo document, SourceSpan span, Expr body)
        {
            if (document == null)
                return body;
            return new SpansExpression(document, span, body);
        }
        
        /// <summary>
        /// Creates a wrapper around a function which represents entry into a chunk of code
        /// </summary>
        public static Expr ExecutionContext(CodeContext context, Expression scopeVariable, SourceUnit source, Expression body)
        {
            return new ExecutionContextExpression(context, scopeVariable, source, body);
        }

        /// <summary>
        /// Creates a wrapper around a block of code representing entry in a scope's domain, and the subsequent exit
        /// </summary>
        public static Expr Scope(CodeContext context, LuaScope scope, Expression body)
        {
            return new ScopeExpression(context, scope, body);
        }
        
        /// <summary>
        /// Creates a wrapper around a variable access expression which will register it
        /// </summary>
        public static Expr VariableAccess(CodeContext context, Expression variable, CodeContext.VariableAccess access)
        {
            return new VariableAccessExpression(context, variable, access);
        }
    }
}
