using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using IronLua.Runtime.Binder;
using IronLua.Util;
using Expr = System.Linq.Expressions.Expression;
using ExprType = System.Linq.Expressions.ExpressionType;
using IronLua.Hosting;
using System.Collections.ObjectModel;
using System.Collections;
using IronLua.Library;

namespace IronLua.Runtime
{
#if DEBUG
    [DebuggerTypeProxy(typeof(LuaTableDebugView))]
#endif
    class LuaTable : IDynamicMetaObjectProvider, IDictionary<string,object>, IDictionary<object,object>
    {
        int[] buckets;
        Entry[] entries;
        int freeList;
        int freeCount;
        int count;

        public CodeContext Context
        { get; set; }

        public LuaTable()
            : this(null)
        {

        }

        public LuaTable(CodeContext context)
        {
            Context = context;

            const int prime = 3;

            buckets = new int[prime];
            for (var i = 0; i < buckets.Length; i++)
                buckets[i] = -1;

            entries = new Entry[prime];
            freeList = -1;
        }

        public LuaTable Metatable { get; set; }

        public DynamicMetaObject GetMetaObject(Expr parameter)
        {
            return new MetaTable(parameter, BindingRestrictions.Empty, this);
        }

        #region LuaTable Methods

        private KeyValuePair<object, int>? LastIndex = null;

        internal Varargs Next(object index = null)
        {
            if (index == null)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Key != null)
                        return new Varargs(entries[i].Key, entries[i].Value);
                }
            }
            else
            {
                for (var i = FindEntry(index) + 1; i < entries.Length; i++)
                {
                    if (entries[i].Key != null)
                        return new Varargs(entries[i].Key, entries[i].Value);
                }
            }
            return null;
        }

        internal object SetValue(object key, object value)
        {
            if (key == null)
                return null;

            if (value == null)
            {
                Remove(key);
                return null;
            }


            //Update existing value

            //  - last used index

            if (LastIndex.HasValue && LastIndex.Value.Key.Equals(key))
            {
                entries[LastIndex.Value.Value].Value = value;
                return value;
            }

            //  - Lookup index

            var hashCode = key.GetHashCode() & Int32.MaxValue;
            var modHashCode = hashCode % buckets.Length;

            for (var i = buckets[modHashCode]; i >= 0; i = entries[i].Next)
            {
                if (entries[i].HashCode == hashCode && entries[i].Key.Equals(key))
                {
                    if (entries[i].Locked)
                        throw LuaRuntimeException.Create(Context, "Cannot change the value of the constant {0}", key);
                    entries[i].Value = value;
                    LastIndex = new KeyValuePair<object, int>(key, i);
                    return value;
                }
            }

            //Add new value

            int free;
            if (freeCount > 0)
            {
                free = freeList;
                freeList = entries[free].Next;
                freeCount--;
            }
            else
            {
                if (count == entries.Length)
                {
                    Resize();
                    modHashCode = hashCode % buckets.Length;
                }
                free = count;
                count++;
            }

            entries[free].HashCode = hashCode;
            entries[free].Next = buckets[modHashCode];
            entries[free].Key = key;
            entries[free].Value = value;
            buckets[modHashCode] = free;
            LastIndex = new KeyValuePair<object, int>(key, free);
            return value;
        }

        internal object SetConstant(object key, object value)
        {
            if (key == null)
                return null;

            if (value == null)
            {
                Remove(key);
                return null;
            }


            var hashCode = key.GetHashCode() & Int32.MaxValue;
            var modHashCode = hashCode % buckets.Length;

            for (var i = buckets[modHashCode]; i >= 0; i = entries[i].Next)
            {
                if (entries[i].HashCode == hashCode && entries[i].Key.Equals(key))
                {
                    if(entries[i].Locked)
                        throw LuaRuntimeException.Create(Context, "The constant {0} is already set to {1} and cannot be modified", key, value);
                    else
                    {
                        //TODO: Decide whether or not we should allow a variable to be converted into a constant
                        entries[i].Value = value;
                        entries[i].Locked = true;
                        return value;
                    }
                }
            }

            int free;
            if (freeCount > 0)
            {
                free = freeList;
                freeList = entries[free].Next;
                freeCount--;
            }
            else
            {
                if (count == entries.Length)
                {
                    Resize();
                    modHashCode = hashCode % buckets.Length;
                }
                free = count;
                count++;
            }

            entries[free].HashCode = hashCode;
            entries[free].Next = buckets[modHashCode];
            entries[free].Key = key;
            entries[free].Value = value;
            entries[free].Locked = true;
            buckets[modHashCode] = free;
            LastIndex = new KeyValuePair<object, int>(key, free);
            return value;
        }

        internal object GetValue(object key)
        {
            if (LastIndex.HasValue && LastIndex.Value.Key.Equals(key))
                return entries[LastIndex.Value.Value].Value;

            var pos = FindEntry(key);
            return pos < 0 ? null : entries[pos].Value;
        }

        internal bool HasValue(object key)
        {            
            var pos = FindEntry(key);
            return pos >= 0;
        }

        public void Remove(object key)
        {
            if (key == null)
                return;

            //We may be able to improve this, only setting it to null when we clear
            //the current key or when we resize the collection.
            LastIndex = null;

            var hashCode = key.GetHashCode() & Int32.MaxValue;
            var modHashCode = hashCode % buckets.Length;
            var last = -1;

            if (LastIndex.HasValue && LastIndex.Value.Key.Equals(key))
            {
                int i = LastIndex.Value.Value;
                if (last < 0)
                    buckets[modHashCode] = entries[i].Next;
                else
                    entries[last].Next = entries[i].Next;

                entries[i].HashCode = -1;
                entries[i].Next = freeList;
                entries[i].Key = null;
                entries[i].Value = null;
                freeList = i;
                freeCount++;
                return;
            }


            for (var i = buckets[modHashCode]; i >= 0; i = entries[i].Next)
            {
                if (entries[i].HashCode == hashCode && entries[i].Key.Equals(key))
                {
                    if (last < 0)
                        buckets[modHashCode] = entries[i].Next;
                    else
                        entries[last].Next = entries[i].Next;

                    entries[i].HashCode = -1;
                    entries[i].Next = freeList;
                    entries[i].Key = null;
                    entries[i].Value = null;
                    freeList = i;
                    freeCount++;
                    return;
                }
                last = i;
            }
        }

        int FindEntry(object key)
        {
            if (key == null)
                return -1;

            var hashCode = key.GetHashCode() & Int32.MaxValue;
            var modHashCode = hashCode % buckets.Length;

            for (var i = buckets[modHashCode]; i >= 0; i = entries[i].Next)
            {
                if (entries[i].HashCode == hashCode && entries[i].Key.Equals(key))
                    return i;
            }

            return -1;
        }

        void Resize()
        {
            var prime = HashHelpers.GetPrime(count * 2);

            var newBuckets = new int[prime];
            for (var i = 0; i < newBuckets.Length; i++)
                newBuckets[i] = -1;

            var newEntries = new Entry[prime];
            Array.Copy(entries, 0, newEntries, 0, count);
            for (var i = 0; i < count; i++)
            {
                var modHashCode = newEntries[i].HashCode % prime;
                newEntries[i].Next = newBuckets[modHashCode];
                newBuckets[modHashCode] = i;
            }

            buckets = newBuckets;
            entries = newEntries;
        }

        /// <summary>
        /// Gets the total number of sequentially indexed values in the table (ignoring non-integer keys)
        /// </summary>
        /// <returns>Returns the number of sequentially indexed values in the table</returns>
        internal int Length()
        {
            var lastNum = 0;
            foreach (var key in entries.Select(e => e.Key).OfType<double>().OrderBy(key => key))
            {
                var intKey = (int)key;

                if (intKey > lastNum + 1)
                    return lastNum;
                if (intKey != key)
                    continue;
                
                lastNum = intKey;
            }
            return lastNum;
        }

        /// <summary>
        /// Gets the total number of elements in the table
        /// </summary>
        internal int Count()
        {
            return entries.Count(x => x.Key != null);
        }

        #endregion

        #region IDictionary Methods

        #region Object Keys

        /// <inheritdoc/>
        public void Add(object key, object value)
        {
            SetValue(key, value);
        }

        /// <inheritdoc/>
        public bool ContainsKey(object key)
        {
            return HasValue(key);
        }

        private IEnumerable<object> _keys()
        {
            Varargs current = null;
            while ((current = Next(current)) != null)
                yield return current.First();
        }

        /// <inheritdoc/>
        public ICollection<object> Keys
        {
            get 
            {
                return new ReadOnlyCollection<object>(_keys().ToList());                
            }
        }

        /// <inheritdoc/>
        bool IDictionary<object, object>.Remove(object key)
        {
            return SetValue(key, null) == null;
        }

        /// <inheritdoc/>
        public bool TryGetValue(object key, out object value)
        {
            value = GetValue(key);
            return value != null;
        }

        private IEnumerable<object> _values()
        {
            Varargs current = null;
            while ((current = Next(current)) != null)
                yield return current.Last();
        }

        /// <inheritdoc/>
        public ICollection<object> Values
        {
            get { return new ReadOnlyCollection<object>(_values().ToList()); }
        }

        /// <inheritdoc/>
        public object this[object key]
        {
            get
            {
                if (key.GetType() == typeof(int))
                    return GetValue(Convert.ToDouble(key));

                return GetValue(key);
            }
            set
            {
                if (key.GetType() == typeof(int))
                    SetValue(Convert.ToDouble(key), value);
                else
                    SetValue(key, value);
            }
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<object, object> item)
        {
            SetValue(item.Key, item.Value);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            Varargs current = null;
            while ((current = Next()) != null)
                Remove(current.First());
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<object, object> item)
        {
            return (HasValue(item.Key) && GetValue(item.Key).Equals(item.Value)) || (!HasValue(item.Key) && item.Value == null);
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex)
        {
            Varargs current = null;
            while ((current = Next(current)) != null)
                array[arrayIndex++] = new KeyValuePair<object, object>(current.First(), current.Last());
        }

        /// <inheritdoc/>
        int ICollection<KeyValuePair<object, object>>.Count
        {
            get { return Count(); }
        }

        /// <inheritdoc/>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<object, object> item)
        {
            if (HasValue(item.Key) && GetValue(item.Key).Equals(item.Value))
            {
                Remove(item.Key);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
        {
            return new LuaTableEnumerator(this);
        }

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new LuaTableEnumerator(this);            
        }

        #endregion

        #region String Keys

        /// <inheritdoc/>
        public void Add(string key, object value)
        {
            double temp = 0;
            if (double.TryParse(key, out temp))
                SetValue(temp, value);
            else
                SetValue(key, value);
        }

        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            double temp = 0;
            bool hasValue = false;
            if (double.TryParse(key, out temp))
                hasValue = HasValue(temp);
            return hasValue || HasValue(key);
        }


        private IEnumerable<string> _keyss()
        {
            Varargs current = null;
            while ((current = Next(current)) != null)
                if(current.First() is string)
                    yield return current.First() as string;
        }

        /// <inheritdoc/>
        ICollection<string> IDictionary<string, object>.Keys
        {
            get { return new ReadOnlyCollection<string>(_keyss().ToList()); }
        }

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            double temp = 0;
            if (double.TryParse(key, out temp))
            {
                if (HasValue(temp))
                {
                    Remove(temp as object);
                    return true;
                }
            }

            if (HasValue(key))
            {
                Remove(key as object);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, out object value)
        {
            double temp = 0;
            if (double.TryParse(key, out temp))
                value = GetValue(temp);
            else
                value = GetValue(key);
            return value != null;
        }

        /// <inheritdoc/>
        public object this[string key]
        {
            get
            {
                object value = null; ;
                if (TryGetValue(key, out value))
                    return value;
                return null;
            }
            set
            {
                double temp = 0;
                if (double.TryParse(key, out temp))
                    SetValue(temp, value);
                else
                    SetValue(key, value);
            }
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<string, object> item)
        {
            SetValue(item.Key, item.Value);
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, object> item)
        {
            return HasValue(item.Key);
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        int ICollection<KeyValuePair<string, object>>.Count
        {
            get { return Count(); }
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, object> item)
        {
            return Remove(item.Key);
        }

        /// <inheritdoc/>
        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return new LuaTableEnumerator(this);
        }

        #endregion

        #endregion

        struct Entry
        {
            public int HashCode;
            public object Key;
            public object Value;
            public int Next;
            public bool Locked;
        }

        class LuaTableEnumerator :  IEnumerator, 
                                    IEnumerator<KeyValuePair<object, object>>, 
                                    IEnumerator<KeyValuePair<string, object>>, 
                                    IEnumerator<Varargs>
        {
            private readonly LuaTable Table;
            private Varargs current;
            bool hasEnumerated = false;

            internal LuaTableEnumerator(LuaTable table)
            {
                Table = table;
            }

            public object Current
            {
                get 
                {
                    if (!hasEnumerated)
                        throw new InvalidOperationException("Must call MoveNext before retrieving current value");
                    return current; 
                }
            }

            public bool MoveNext()
            {
                hasEnumerated = true;
                current = Table.Next(current);
                return current != null;
            }

            public void Reset()
            {
                current = null;
                hasEnumerated = false;
            }

            KeyValuePair<object, object> IEnumerator<KeyValuePair<object, object>>.Current
            {
                get
                {
                    if (!hasEnumerated)
                        throw new InvalidOperationException("Must call MoveNext before retrieving current value"); 
                    return new KeyValuePair<object, object>(current.First(), current.Last());
                }
            }

            public void Dispose()
            {
                
            }

            Varargs IEnumerator<Varargs>.Current
            {
                get
                {
                    if (!hasEnumerated)
                        throw new InvalidOperationException("Must call MoveNext before retrieving current value");
                    return current;
                }
            }

            KeyValuePair<string, object> IEnumerator<KeyValuePair<string, object>>.Current
            {
                get
                {
                    if (!hasEnumerated)
                        throw new InvalidOperationException("Must call MoveNext before retrieving current value"); 
                    return new KeyValuePair<string, object>(
                        current.First() is string ? current.First() as string : ("<<" + BaseLibrary.ToStringEx(current.First()) + ">>"),
                        current.Last());
                }
            }
        }

        class MetaTable : DynamicMetaObject
        {
            public MetaTable(Expr expression, BindingRestrictions restrictions, LuaTable value)
                : base(expression, restrictions, value)
            {

            }

            public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
            {
                if (!LuaBinaryOperationBinder.BinaryExprTypes.ContainsKey(binder.Operation))
                    return LuaRuntimeException.CreateDMO(Value as LuaTable, "operation {0} not defined for table", binder.Operation.ToString());

                var expression = MetamethodFallbacks.WrapStackTrace(MetamethodFallbacks.BinaryOp(Value as LuaTable, binder.Operation, this, arg), Value as LuaTable, 
                    new FunctionStack(LuaOps.GetMethodName(binder.Operation)));

                return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));
            }

            public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
            {
                if (binder.Operation != ExprType.Negate)
                    return LuaRuntimeException.CreateDMO(Value as LuaTable, "operation {0} not defined for table", binder.Operation.ToString());

                var expression = MetamethodFallbacks.WrapStackTrace(MetamethodFallbacks.UnaryMinus(Value as LuaTable, this), Value as LuaTable,
                    new FunctionStack(LuaOps.GetMethodName(binder.Operation)));

                return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));
            }

            public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
            {
                var expression = MetamethodFallbacks.WrapStackTrace(MetamethodFallbacks.Call(Value as LuaTable, this, args), Value as LuaTable,
                    new FunctionStack(Constant.CALL_METAMETHOD));
                return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));
            }

            public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
            {
                var expression = Expr.Dynamic(new LuaGetMemberBinder((Value as LuaTable).Context, binder.Name), typeof(object), Expression);
                return binder.FallbackInvoke(new DynamicMetaObject(expression, Restrictions), args, null);
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                //var expression = Expr.Call(
                //    Expr.Convert(Expression, typeof(LuaTable)),
                //    MemberInfos.LuaTableGetValue,
                //    Expr.Constant(binder.Name));

                //return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));

                var valueVar = Expr.Variable(typeof(object), "$bindgetindex_valueVar$");

                var getValue = Expr.Call(
                    Expr.Convert(Expression, typeof(LuaTable)),
                    MemberInfos.LuaTableGetValue,
                    Expr.Convert(Expr.Constant(binder.Name), typeof(object)));
                var valueAssign = Expr.Assign(valueVar, getValue);

                var expression = Expr.Block(
                    new[] { valueVar },
                    valueAssign,
                    Expr.Condition(
                        Expr.Equal(valueVar, Expr.Constant(null)),
                        
                        MetamethodFallbacks.WrapStackTrace(
                            MetamethodFallbacks.Index(Value as LuaTable, this, 
                                new[] { new DynamicMetaObject(Expr.Constant(binder.Name), BindingRestrictions.Empty, binder.Name) }), 
                            Value as LuaTable,
                            new FunctionStack(Constant.INDEX_METAMETHOD)),
                        valueVar));

                return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                //var expression = Expr.Call(
                //    Expr.Convert(Expression, typeof(LuaTable)),
                //    MemberInfos.LuaTableSetValue,
                //    Expr.Constant(binder.Name),
                //    Expr.Convert(value.Expression, typeof(object)));

                //return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));

                var getValue = Expr.Call(
                    Expr.Convert(Expression, typeof(LuaTable)),
                    MemberInfos.LuaTableGetValue,
                    Expr.Convert(Expr.Constant(binder.Name), typeof(object)));

                var setValue = Expr.Call(
                    Expr.Convert(Expression, typeof(LuaTable)),
                    MemberInfos.LuaTableSetValue,
                    Expr.Convert(Expr.Constant(binder.Name), typeof(object)),
                    Expr.Convert(value.Expression, typeof(object)));

                var expression = Expr.Condition(
                    Expr.Equal(getValue, Expr.Constant(null)),
                    MetamethodFallbacks.WrapStackTrace(
                        MetamethodFallbacks.NewIndex(Value as LuaTable, this, new[] { new DynamicMetaObject(Expr.Constant(binder.Name), BindingRestrictions.Empty, binder.Name) }, value), Value as LuaTable,
                        new FunctionStack(Constant.INDEX_METAMETHOD)),
                    setValue);

                return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));
            }

            public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
            {
                var valueVar = Expr.Variable(typeof(object),"$bindgetindex_valueVar$");

                var getValue = Expr.Call(
                    Expr.Convert(Expression, typeof(LuaTable)),
                    MemberInfos.LuaTableGetValue,
                    Expr.Convert(indexes[0].Expression, typeof(object)));
                var valueAssign = Expr.Assign(valueVar, getValue);

                var expression = Expr.Block(
                    new[] { valueVar },
                    valueAssign,
                    Expr.Condition(
                        Expr.Equal(valueVar, Expr.Constant(null)),
                        MetamethodFallbacks.WrapStackTrace(
                            MetamethodFallbacks.Index(Value as LuaTable, this, indexes), Value as LuaTable,
                            new FunctionStack(Constant.INDEX_METAMETHOD)),
                        valueVar));

                return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));
            }

            public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
            {
                var getValue = Expr.Call(
                    Expr.Convert(Expression, typeof(LuaTable)),
                    MemberInfos.LuaTableGetValue,
                    Expr.Convert(indexes[0].Expression, typeof(object)));

                var setValue = Expr.Call(
                    Expr.Convert(Expression, typeof(LuaTable)),
                    MemberInfos.LuaTableSetValue,
                    Expr.Convert(indexes[0].Expression, typeof(object)),
                    Expr.Convert(value.Expression, typeof(object)));

                var expression = Expr.Condition(
                    Expr.Equal(getValue, Expr.Constant(null)),
                    MetamethodFallbacks.WrapStackTrace(
                        MetamethodFallbacks.NewIndex(Value as LuaTable, this, indexes, value), Value as LuaTable,
                        new FunctionStack(Constant.INDEX_METAMETHOD)),
                    setValue);

                return new DynamicMetaObject(expression, RuntimeHelpers.MergeTypeRestrictions(this));
            }
        }

#if DEBUG
        class LuaTableDebugView
        {
            LuaTable table;

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePair<object, object>[] Items
            {
                get
                {
                    var index = 0;
                    var pairs = new KeyValuePair<object, object>[table.count];

                    for (var i = 0; i < table.count; i++)
                    {
                        if (table.entries[i].HashCode >= 0)
                            pairs[index++] = new KeyValuePair<object, object>(table.entries[i].Key,
                                                                              table.entries[i].Value);
                    }

                    return pairs;
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
            public CodeContext Code
            {
                get
                {
                    return table.Context;
                }
            }

            public LuaTableDebugView(LuaTable table)
            {
                this.table = table;
            }
        }
#endif

    }
}
