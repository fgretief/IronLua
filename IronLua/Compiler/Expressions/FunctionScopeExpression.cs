using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using IronLua.Runtime;

namespace IronLua.Compiler.Expressions
{
    class FunctionScopeExpression : Expression
    {
        private readonly CodeContext _context;
        private readonly LuaScope _scope;   //The scope that contains our function's local values
        private readonly LuaScope _upscope; //The scope that contains our function's up-values
        private readonly Expression _body;
        private readonly IEnumerable<string> _identifiers;  //Holds the identifiers for the function (name)

        /// <summary>
        /// Creates a wrapper around a function which represents entry into its scope
        /// </summary>
        /// <param name="context">The context under which this function is compiled</param>
        /// <param name="body">The block expression which represents this function's execution</param>
        /// <param name="evalScope">The scope in which the body will be executed, holding local values</param>
        /// <param name="upScope">The scope in which the function will execute, storing up values (which persist between calls)</param>
        public FunctionScopeExpression(CodeContext context, LuaScope evalScope, LuaScope upScope, IEnumerable<string> identifiers, Expression body)
        {
            _context = context;
            _body = body;
            _scope = evalScope;
            _upscope = upScope;
            _identifiers = identifiers;
        }

        public override bool CanReduce
        {
            get
            {
                return true;
            }
        }

        public override ExpressionType NodeType
        {
            get
            {
                return ExpressionType.Extension;
            }
        }

        public Expression Body
        {
            get
            {
                return _body;
            }
        }

        public override Expression Reduce()
        {
            return Expression.TryFinally(
                Expression.Block(
                    Expression.Assign(
                        CodeContext.FunctionStackVariable,
                        CodeContext.PushFunctionStack(_context, new FunctionStack(_context, _upscope, _scope))
                    ),
                    _body),
                CodeContext.PopFunctionStack(_context)
            );
        }

        public override Type Type
        {
            get
            {
                return _body.Type;
            }
        }
    }
}
