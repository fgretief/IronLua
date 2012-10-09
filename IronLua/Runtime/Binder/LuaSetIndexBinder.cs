using System.Diagnostics.Contracts;
using System.Dynamic;

namespace IronLua.Runtime.Binder
{
    class LuaSetIndexBinder : SetIndexBinder
    {
        private readonly CodeContext _context;

        public LuaSetIndexBinder(CodeContext context, CallInfo callInfo)
            : base(callInfo)
        {
            Contract.Requires(context != null);
            _context = context;
        }

        public LuaSetIndexBinder(CodeContext context)
            : this(context, new CallInfo(1))
        {
        }

        public CodeContext Context
        {
            get { return _context; }
        }
        
        public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
        {
            var expression = MetamethodFallbacks.WrapStackTrace(MetamethodFallbacks.NewIndex(_context, target, indexes, value), Context,
                    new FunctionStack(Context, null, null, Constant.NEWINDEX_METAMETHOD));

            return new DynamicMetaObject(expression, BindingRestrictions.Empty);
        }
    }
}