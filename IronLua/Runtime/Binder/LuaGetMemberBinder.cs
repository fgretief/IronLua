using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using System;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq.Expressions;

namespace IronLua.Runtime.Binder
{
    class LuaGetMemberBinder : GetMemberBinder
    {
        private readonly CodeContext _context;

        public LuaGetMemberBinder(CodeContext context, string name, bool ignoreCase = false)
            : base(name, ignoreCase)
        {
            _context = context;
        }

        DynamicMetaObject MakeScriptScopeGetMember(DynamicMetaObject target, string name)
        {
            var getMemberExpression = Expression.PropertyOrField(Expression.Convert(target.Expression, target.LimitType), name);
            return new DynamicMetaObject(getMemberExpression, BindingRestrictions.Empty);
        }

        private DynamicMetaObject WrapToObject(DynamicMetaObject obj)
        {
            if (obj.LimitType != typeof(object))
                return new DynamicMetaObject(Expression.Convert(obj.Expression, typeof(object)), obj.Restrictions, obj.Value as object);
            return obj;
        }

        public override DynamicMetaObject FallbackGetMember(
            DynamicMetaObject target, 
            DynamicMetaObject errorSuggestion)
        {
            // Defer if any object has no value so that we evaulate their
            // Expressions and nest a CallSite for the InvokeMember.
            if (!target.HasValue) 
                return Defer(target);


            if (target.LimitType == typeof(IDynamicMetaObjectProvider))
                return new DefaultBinder().GetMember(Name, target);
            

            return WrapToObject(_context.Binder.GetMember(Name, target));            
        }
    }
}