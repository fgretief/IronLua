using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using IronLua.Runtime.Binder;
using Microsoft.Scripting.Utils;
using System.Runtime.CompilerServices;
using System.Threading;

namespace IronLua.Runtime
{
    // TODO: Make thread-safe
    class DynamicCache
    {
        readonly CodeContext context;
        static readonly Dictionary<ExpressionType, LuaBinaryOperationBinder> binaryOperationBinders;
        static readonly Dictionary<ExpressionType, LuaUnaryOperationBinder> unaryOperationBinders;
        static readonly Dictionary<InvokeMemberBinderKey, LuaInvokeMemberBinder> invokeMemberBinders;
        static readonly Dictionary<CallInfo, LuaInvokeBinder> invokeBinders;
        static readonly Dictionary<Type, LuaConvertBinder> convertBinders;
        static readonly Dictionary<string, LuaSetMemberBinder> setMemberBinders;
        static readonly Dictionary<string, LuaGetMemberBinder> getMemberBinders;
        LuaSetIndexBinder setIndexBinder;
        LuaGetIndexBinder getIndexBinder;

        CallSite<Func<object, object, object>> getDynamicIndexCache = null;
        CallSite<Func<object, object, object, object>> getDynamicNewIndexCache = null;
        CallSite<Func<object, object>> getDynamicCallCache0 = null;
        CallSite<Func<object, object, object>> getDynamicCallCache1 = null;
        CallSite<Func<object, object, object, object>> getDynamicCallCache2;
        CallSite<Func<object, object, object, object, object>> getDynamicCallCache3;

        static DynamicCache()
        {
            binaryOperationBinders = new Dictionary<ExpressionType, LuaBinaryOperationBinder>();
            unaryOperationBinders = new Dictionary<ExpressionType, LuaUnaryOperationBinder>();
            invokeMemberBinders = new Dictionary<InvokeMemberBinderKey, LuaInvokeMemberBinder>();
            invokeBinders = new Dictionary<CallInfo, LuaInvokeBinder>();
            convertBinders = new Dictionary<Type, LuaConvertBinder>();
            setMemberBinders = new Dictionary<string, LuaSetMemberBinder>();
            getMemberBinders = new Dictionary<string, LuaGetMemberBinder>();
        }

        public DynamicCache(CodeContext context)
        {
            ContractUtils.RequiresNotNull(context, "context");
            this.context = context;
        }

        public BinaryOperationBinder GetBinaryOperationBinder(ExpressionType operation)
        {
            return GetCachedBinder(binaryOperationBinders, operation, k => new LuaBinaryOperationBinder(context, k));
        }

        public UnaryOperationBinder GetUnaryOperationBinder(ExpressionType operation)
        {
            return GetCachedBinder(unaryOperationBinders, operation, k => new LuaUnaryOperationBinder(context, k));
        }

        public InvokeMemberBinder GetInvokeMemberBinder(string name, CallInfo info)
        {
            return GetCachedBinder(invokeMemberBinders, new InvokeMemberBinderKey(name, info),
                                   k => new LuaInvokeMemberBinder(context, k.Name, k.Info));
        }

        public InvokeBinder GetInvokeBinder(CallInfo callInfo)
        {
            return GetCachedBinder(invokeBinders, callInfo, k => new LuaInvokeBinder(context, k));
        }

        public ConvertBinder GetConvertBinder(Type type)
        {
            return GetCachedBinder(convertBinders, type, k => new LuaConvertBinder(context, k));
        }

        public SetMemberBinder GetSetMemberBinder(string name)
        {
            return GetCachedBinder(setMemberBinders, name, k => new LuaSetMemberBinder(context, k));
        }

        public SetIndexBinder GetSetIndexBinder()
        {
            return setIndexBinder ?? (setIndexBinder = new LuaSetIndexBinder(context));
        }

        public GetMemberBinder GetGetMemberBinder(string name)
        {
            return GetCachedBinder(getMemberBinders, name, k => new LuaGetMemberBinder(context,k));
        }

        public GetIndexBinder GetGetIndexBinder()
        {
            return getIndexBinder ?? (getIndexBinder = new LuaGetIndexBinder(context));
        }

        TValue GetCachedBinder<TKey, TValue>(Dictionary<TKey, TValue> cache, TKey key, Func<TKey, TValue> newer)
        {
            TValue binder;
            if (cache.TryGetValue(key, out binder))
                return binder;
            return cache[key] = newer(key);
        }

        // Stolen from DLR's reference implementation Sympl
        class InvokeMemberBinderKey
        {
            public string Name { get; private set; }
            public CallInfo Info { get; private set; }

            public InvokeMemberBinderKey(string name, CallInfo info)
            {
                Name = name;
                Info = info;
            }

            public override bool Equals(object obj)
            {
                var key = obj as InvokeMemberBinderKey;
                return key != null && Name == key.Name && Info.Equals(key.Info);
            }

            public override int GetHashCode()
            {
                return 0x28000000 ^ Name.GetHashCode() ^ Info.GetHashCode();
            }
        }

        public CallSite<Func<object, object, object>> GetDynamicIndex()
        {
            //var objVar = Expression.Parameter(typeof(object));
            //var keyVar = Expression.Parameter(typeof(object));
            //var expr = Expression.Lambda<Func<object, object, object>>(
            //    Expression.Dynamic(this.GetGetIndexBinder(), typeof(object), objVar, keyVar),
            //    objVar, keyVar);

            //return getDynamicIndexCache = expr.Compile();

            if (getDynamicNewIndexCache == null)
                Interlocked.CompareExchange(ref getDynamicIndexCache, CallSite<Func<object, object, object>>.Create(GetGetIndexBinder()), null);
            return getDynamicIndexCache;
        }

        public CallSite<Func<object, object, object, object>> GetDynamicNewIndex()
        {
            //var objVar = Expression.Parameter(typeof(object));
            //var keyVar = Expression.Parameter(typeof(object));
            //var valueVar = Expression.Parameter(typeof(object));
            //var expr = Expression.Lambda<Func<object, object, object, object>>(
            //    Expression.Dynamic(this.GetSetIndexBinder(), typeof(object), objVar, keyVar, valueVar),
            //    objVar, keyVar, valueVar);

            //return getDynamicNewIndexCache = expr.Compile();

            if(getDynamicNewIndexCache == null)
                Interlocked.CompareExchange(ref getDynamicNewIndexCache, CallSite<Func<object, object, object, object>>.Create(GetSetIndexBinder()), null);
            return getDynamicNewIndexCache;
        }

        public CallSite<Func<object, object>> GetDynamicCall0()
        {
            if(getDynamicCallCache0 == null)
                Interlocked.CompareExchange(ref getDynamicCallCache0, 
                    CallSite<Func<object, object>>.Create(GetInvokeBinder(new CallInfo(0)))
                    , null);
            return getDynamicCallCache0;
        }

        public CallSite<Func<object, object, object>> GetDynamicCall1()
        {
            if (getDynamicCallCache1 == null)
                Interlocked.CompareExchange(ref getDynamicCallCache1,
                    CallSite<Func<object, object, object>>.Create(GetInvokeBinder(new CallInfo(1)))
                    , null);
            return getDynamicCallCache1;
        }

        public CallSite<Func<object, object, object, object>> GetDynamicCall2()
        {            
            if (getDynamicCallCache2 == null)
            {
                var temp = CallSite<Func<object, object, object, object>>.Create(GetInvokeBinder(new CallInfo(2)));
                Interlocked.CompareExchange(ref getDynamicCallCache2,
                    temp
                    , null);
            }
            return getDynamicCallCache2;
        }

        public CallSite<Func<object, object, object, object, object>> GetDynamicCall3()
        {
            if (getDynamicCallCache3 == null)
                Interlocked.CompareExchange(ref getDynamicCallCache3,
                    CallSite<Func<object, object, object, object, object>>.Create(GetInvokeBinder(new CallInfo(3)))
                    , null);
            return getDynamicCallCache3;
        }
    }
}
