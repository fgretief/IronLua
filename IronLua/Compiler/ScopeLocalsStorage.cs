using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using IronLua.Runtime;

namespace IronLua.Compiler
{
    class ScopeLocalsStorage : IDynamicMetaObjectProvider, IEnumerable<KeyValuePair<string,object>>
    {
        private Dictionary<string, object> _storage = null;
        private LinkedList<string> _names = new LinkedList<string>();

        private static readonly MethodInfo SLSSetMember = typeof(ScopeLocalsStorage).GetMethod("SetMember");
        private static readonly MethodInfo SLSGetMember = typeof(ScopeLocalsStorage).GetMethod("GetMember");
        private static readonly MethodInfo SLSClearValues = typeof(ScopeLocalsStorage).GetMethod("Clean");
        public static Expression ClearValues(LuaScope storage)
        {
            return Expression.Empty();
            //return Expression.Call(Expression.Constant(storage.variables), SLSClearValues);
        }

        public ScopeLocalsStorage()
        {

        }

        public void OnCompilationCompleted()
        {
            _storage = new Dictionary<string, object>(_names.Count);
            foreach (var n in _names)
                _storage.Add(n, null);
            _names.Clear();
            _names = null;
        }

        public DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression parameter)
        {
            return new MetaObject(this, parameter);
        }

        public Func<Expression,Expression> RegisterLocal(CodeContext context, string name)
        {
            _names.AddLast(name);
            return value => Expression.Call(Expression.Constant(this), SLSSetMember, Expression.Constant(name), value);
        }

        public bool TryGetRegisteredLocalRead(CodeContext context, string name, out Expression expression)
        {
            expression = Expression.Empty();
            if (!_names.Contains(name))
                return false;

            expression = Expression.Call(Expression.Constant(this), SLSGetMember, Expression.Constant(name));
            return true;
        }

        public bool TryGetRegisteredLocalWrite(CodeContext context, string name, out Func<Expression,Expression> writer)
        {
            writer = null;
            if (!_names.Contains(name))
                return false;

            writer = value => Expression.Call(Expression.Constant(this), SLSSetMember, Expression.Constant(name), value);
            return true;
        }

        public object GetMember(string name)
        {
            return _storage.ContainsKey(name) ? _storage[name] : null;
        }

        public object SetMember(string name, object value)
        {
            if (_storage.ContainsKey(name))
                _storage[name] = value;
            else
                _storage.Add(name, value);
            return value;
        }

        public bool TryGetMember(string name, out object value)
        {
            value = GetMember(name);
            return value != null;
        }

        public void Clean()
        {
            _names = new LinkedList<string>();
            foreach (var v in _storage.Keys)
                _names.AddLast(v);
            _storage.Clear();
            foreach (var v in _names)
                _storage.Add(v, null);
            _names.Clear();
            _names = null;
        }

        public int Count()
        {
            if (_names != null)
                return _names.Count;
            else if (_storage != null)
                return _storage.Count;
            return 0;
        }

        class MetaObject : DynamicMetaObject
        {
            private readonly ScopeLocalsStorage _store;
            private static readonly MethodInfo SLSGetMember = typeof(ScopeLocalsStorage).GetMethod("GetValue");
            private static readonly MethodInfo SLSSetMember = typeof(ScopeLocalsStorage).GetMethod("SetValue");

            public MetaObject(ScopeLocalsStorage store, Expression parameter)
                : base(parameter, BindingRestrictions.GetTypeRestriction(parameter, parameter.Type), store)
            {
                _store = store;
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                var exp = Expression.Call(base.Expression, SLSGetMember, Expression.Constant(binder.Name));

                return new DynamicMetaObject(exp, BindingRestrictions.GetExpressionRestriction(Expression.Constant(binder.Name)));
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                var exp = Expression.Call(base.Expression, SLSSetMember, Expression.Constant(binder.Name), value.Expression);

                return new DynamicMetaObject(exp, BindingRestrictions.GetExpressionRestriction(Expression.Constant(binder.Name)));
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _storage.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _storage.GetEnumerator();
        }
    }
}
