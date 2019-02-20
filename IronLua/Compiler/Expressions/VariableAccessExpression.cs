using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronLua.Runtime;
using System.Linq.Expressions;

namespace IronLua.Compiler.Expressions
{
    class VariableAccessExpression : Expression
    {
        private readonly CodeContext _context;
        private readonly VariableAccess _access;
        private readonly Expression _variable;   //The expression representing the variable access

        /// <summary>
        /// Creates a wrapper around a variable access expression which will register it
        /// </summary>
        public VariableAccessExpression(CodeContext context, Expression variable, VariableAccess access)
        {
            _context = context;
            _variable = variable;
            _access = access;
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

        public Expression Variable
        {
            get
            {
                return _variable;
            }
        }

        public override Expression Reduce()
        {
            var temp = Expression.Parameter(typeof(object));

            return Expression.Block(
                        _variable.Type,
                        new[] { temp },
                        Expression.Assign(temp, _variable.Reduce()),
                        CodeContext.UpdateLastVariableAccess(_context, _access, temp),
                        temp);
        }

        public override Type Type
        {
            get
            {
                return _variable.Type;
            }
        }
    }
}
