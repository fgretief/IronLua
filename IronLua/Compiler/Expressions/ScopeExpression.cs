using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using IronLua.Runtime;

namespace IronLua.Compiler.Expressions
{
    class ScopeExpression : Expression
    {
        private readonly CodeContext _context;
        private readonly LuaScope _scope;   //The scope that is being entered and exited from
        private readonly Expression _body;

        /// <summary>
        /// Creates a wrapper around a block of code representing entry in a scope's domain, and the subsequent exit
        /// </summary>
        public ScopeExpression(CodeContext context, LuaScope scope, Expression body)
        {
            _context = context;
            _body = body;
            _scope = scope;
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
                    CodeContext.OnScopeEnter(_context, _scope),
                    _body),
                CodeContext.OnScopeLeave(_context)
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
