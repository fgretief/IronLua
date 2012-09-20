using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using IronLua.Library;
using System;
using System.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace IronLua.Runtime.Binder
{
    class LuaBinder : DefaultBinder
    {
        private readonly CodeContext _context;

        public LuaBinder(CodeContext context)
        {
            Contract.Requires(context != null);
            _context = context;
        }

        public LuaBinder(LuaBinder binder)
        {
            _context = binder._context;
        }

        public override bool CanConvertFrom(System.Type fromType, System.Type toType, bool toNotNullable, NarrowingLevel level)
        {
            if (fromType == typeof(double) && toType == typeof(string))
                return true;
            else if (fromType == typeof(string) && toType == typeof(double))
                return !toNotNullable;
            
            else if(fromType.GetInterfaces().Any(x => x == typeof(IConvertible)))
                return true;

            return base.CanConvertFrom(fromType, toType, toNotNullable, level);
        }

        public override object Convert(object obj, System.Type toType)
        {
            if (obj is double && toType == typeof(string))
                return BaseLibrary.ToStringEx(obj);

            else if (obj is string && toType == typeof(double))
                return BaseLibrary.ToNumber(_context, obj, 10.0);

            else if (obj.GetType().GetInterfaces().Any(x => x == typeof(IConvertible)))
                return System.Convert.ChangeType(obj, toType);

            return base.Convert(obj, toType);
        }

        public override MemberGroup GetMember(MemberRequestKind action, Type type, string name)
        {
            try
            {
                return base.GetMember(action, type, name);
            }
            catch (Exception ex)
            {
                throw new LuaRuntimeException(_context, string.Format("could not find the specified member on '{0}'", type.FullName), ex);
            }
        }

        public override ErrorInfo MakeMissingMemberErrorInfo(Type type, string name)
        {
            var inner = base.MakeMissingMemberErrorInfo(type, name);

            return ErrorInfo.FromException(ThrowRuntimeError("could not find the field, index, method or property '" + name + "' on '" + type.FullName + "'", inner));
        }

        public override string GetTypeName(Type t)
        {
            return BaseLibrary.TypeName(t);
        }

        private Expr ThrowRuntimeError(string format, params object[] args)
        {
            return Expr.New(MemberInfos.NewRuntimeException, Expr.Constant(_context), Expr.Constant(format), Expr.Constant(args));
        }

        private Expr ThrowRuntimeError(string format, Expr innerEx)
        {
            return Expr.New(MemberInfos.NewRuntimeException, Expr.Constant(_context), Expr.Constant(format), innerEx);
        }
    }

    internal sealed class LuaOverloadResolverFactory : OverloadResolverFactory
    {
        private readonly LuaBinder _binder;

        public LuaOverloadResolverFactory(LuaBinder binder)
        {
            Contract.Requires(binder != null);
            _binder = binder;
        }

        public override DefaultOverloadResolver CreateOverloadResolver(IList<DynamicMetaObject> args, CallSignature signature, CallTypes callType)
        {
            return new LuaOverloadResolver(_binder, args, signature, callType);
        }
    }

    internal sealed class LuaOverloadResolver : DefaultOverloadResolver
    {
        public LuaOverloadResolver(ActionBinder binder, DynamicMetaObject instance, IList<DynamicMetaObject> args, CallSignature signature) 
            : base(binder, instance, args, signature)
        {
        }

        public LuaOverloadResolver(ActionBinder binder, IList<DynamicMetaObject> args, CallSignature signature) 
            : base(binder, args, signature)
        {
        }

        public LuaOverloadResolver(ActionBinder binder, IList<DynamicMetaObject> args, CallSignature signature, CallTypes callType) 
            : base(binder, args, signature, callType)
        {
        }

        private new LuaBinder Binder
        {
            get { return (LuaBinder)base.Binder; }
        }
    }
}