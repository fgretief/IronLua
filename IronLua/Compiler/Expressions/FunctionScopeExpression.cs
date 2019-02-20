using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using IronLua.Runtime;
using System.Reflection;

namespace IronLua.Compiler.Expressions
{
    class FunctionScopeExpression : Expression
    {
        private readonly Expression _context;
        private readonly Expression _body;
        private readonly Expression _stack;

        private readonly LuaScope _evalScope;
        private readonly LuaScope _upScope;


        /// <summary>
        /// Creates a wrapper around a function which represents entry into its scope
        /// </summary>
        /// <param name="context">The context under which this function is compiled</param>
        /// <param name="body">The block expression which represents this function's execution</param>
        public FunctionScopeExpression(Expression context, string identifier, Expression body)
        {
            _context = context;
            _body = body;
            _stack = Expression.Constant(new FunctionStack(identifier));
        }

        /// <summary>
        /// Creates a wrapper around a function which represents entry into its scope
        /// </summary>
        /// <param name="context">The context under which this function is compiled</param>
        /// <param name="body">The block expression which represents this function's execution</param>
        /// <param name="evalScope">The scope in which the body will be executed, holding local values</param>
        /// <param name="upScope">The scope in which the function will execute, storing up values (which persist between calls)</param>
        public FunctionScopeExpression(CodeContext context, LuaScope evalScope, LuaScope upScope, IEnumerable<string> identifiers, Expression body)
        {
            _context = Expression.Constant(context);
            _body = body;
            _stack = Expression.Constant(new FunctionStack(context, upScope, evalScope, identifiers));

            _evalScope = evalScope;
            _upScope = upScope;
        }

        /// <summary>
        /// Creates a wrapper around a function which represents entry into its scope
        /// </summary>
        /// <param name="context">The context under which this function is compiled</param>
        /// <param name="body">The block expression which represents this function's execution</param>
        /// <param name="evalScope">The scope in which the body will be executed, holding local values</param>
        /// <param name="upScope">The scope in which the function will execute, storing up values (which persist between calls)</param>
        public FunctionScopeExpression(CodeContext context, LuaScope evalScope, LuaScope upScope, string identifier, Expression body)
        {
            _context = Expression.Constant(context);
            _body = body;
            _stack = Expression.Constant(new FunctionStack(context, upScope, evalScope, identifier));

            _evalScope = evalScope;
            _upScope = upScope;
        }

        private static readonly ConstructorInfo NewFunctionStack = typeof(FunctionStack).GetConstructor(new[] { typeof(CodeContext), typeof(LuaScope), typeof(LuaScope), typeof(string) });

        /// <summary>
        /// Creates a wrapper around a function which represents entry into its scope
        /// </summary>
        /// <param name="context">The context under which this function is compiled</param>
        /// <param name="body">The block expression which represents this function's execution</param>
        /// <param name="evalScope">The scope in which the body will be executed, holding local values</param>
        /// <param name="upScope">The scope in which the function will execute, storing up values (which persist between calls)</param>
        public FunctionScopeExpression(Expression context, LuaScope evalScope, LuaScope upScope, string identifier, Expression body)
        {
            _context = context;
            _body = body;

            _evalScope = evalScope;
            _upScope = upScope;

            _stack = Expression.New(NewFunctionStack, context, Expression.Constant(evalScope, typeof(LuaScope)), Expression.Constant(upScope, typeof(LuaScope)), Expression.Constant(identifier));
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
            List<Expression> blockExpressions = new List<Expression>();
            blockExpressions.Add(Expression.Assign(
                        CodeContext.FunctionStackVariable,
                        CodeContext.PushFunctionStack(_context, _stack)
                    ));

            if(_evalScope != null)
                blockExpressions.Add(Expression.Assign(
                        Expression.Field(_stack, "LocalVariables"),
                        Expression.RuntimeVariables(_evalScope.GetLocals())
                        ));

            if(_upScope != null)
                blockExpressions.Add(Expression.Assign(
                        Expression.Field(_stack, "UpValues"),
                        Expression.RuntimeVariables(_upScope.GetLocals())
                    ));

            blockExpressions.Add(_body.Reduce());

            return Expression.TryFinally(
                Expression.Block(new[] { CodeContext.FunctionStackVariable }, blockExpressions),
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
