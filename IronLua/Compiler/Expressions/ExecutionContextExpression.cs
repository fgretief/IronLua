using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using IronLua.Runtime;
using Microsoft.Scripting;

namespace IronLua.Compiler.Expressions
{
    class ExecutionContextExpression : Expression
    {
        private readonly CodeContext _context;
        private readonly Expression _scope;   //The scope that contains our function's local values
        private readonly Expression _body;
        private readonly SourceUnit _source;

        /// <summary>
        /// Creates a wrapper around a function which represents entry into a chunk of code
        /// </summary>
        public ExecutionContextExpression(CodeContext context, Expression scopeVariable, SourceUnit source, Expression body)
        {
            _context = context;
            _body = body;
            _scope = scopeVariable;
            _source = source;
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
                    CodeContext.OnStartExecute(_context, _scope, _source),
                    _body.Reduce()),
                CodeContext.OnFinishExecute(_context)
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
