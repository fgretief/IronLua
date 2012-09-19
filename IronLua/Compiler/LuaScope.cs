using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Threading;
using Expr = System.Linq.Expressions.Expression;
using ParamExpr = System.Linq.Expressions.ParameterExpression;
using IronLua.Runtime;

namespace IronLua.Compiler
{
    class LuaScope
    {
        const string BreakLabelName = "@break";
        const string ReturnLabelName = "@return";

        readonly LuaScope parent;
        readonly CodeContext context;
        readonly Dictionary<string, ParamExpr> variables;
        readonly Dictionary<string, LabelTarget> labels;
        
        static int hiddenId;

        public bool IsRoot { get { return parent == null; } }

        private LuaScope(CodeContext context)
        {
            this.context = context;
            this.parent = null;
            this.variables = new Dictionary<string, ParamExpr>();
            this.labels = new Dictionary<string, LabelTarget>();
        }
        
        private LuaScope(LuaScope parent)
        {
            this.context = parent.context;
            this.parent = parent;
            this.variables = new Dictionary<string, ParamExpr>();
            this.labels = new Dictionary<string, LabelTarget>();
        }

        public int LocalsCount
        {
            get { return variables.Count; }
        }

        public ParamExpr[] AllLocals()
        {
            var values = variables.Values;
            var array = new ParamExpr[values.Count];
            values.CopyTo(array, 0);
            return array;
        }

        public IEnumerable<ParamExpr> GetLocals()
        {
            return variables.Values;
        }

        public bool TryGetLocal(string name, out ParamExpr local)
        {
            if (variables.TryGetValue(name, out local))
                return true;
            if (parent != null)
                return parent.TryGetLocal(name, out local);

            return false;
        }

        public ParamExpr AddLocal(string name, Type type = null)
        {
            // We have this behavior so that ex. "local x, x = 1, 2" works
            ParamExpr param;
            if (!variables.TryGetValue(name, out param))
                variables.Add(name, param = Expr.Variable(type ?? typeof(object), name));

            return param;
        }

        public void AddHidden(ParamExpr param)
        {
            var id = Interlocked.Increment(ref hiddenId);
            var key = String.Format("$H{0}", id);
            variables.Add(key, param);
        }

        public LabelTarget AddLabel(string name)
        {
            LabelTarget label;

            if (!labels.TryGetValue(name, out label))
                labels.Add(name, label = Expr.Label(name));

            return label;
        }

        public bool TryGetLabel(string name, out LabelTarget label)
        {
            if (labels.TryGetValue(name, out label))
                return true;

            if (parent != null)
                return parent.TryGetLabel(name, out label);

            return false;
        }

        public LabelTarget GetReturnLabel()
        {
            LabelTarget label;
            return TryGetLabel(ReturnLabelName, out label) ? label : null;
        }

        public LabelTarget BreakLabel()
        {
            return AddLabel(BreakLabelName); 
        }

        public Expr GetDlrGlobals()
        {
            return CodeContext.GetExecutionEnvironment(context, this);
        }

        public static LuaScope CreateRoot(CodeContext context)
        {
            Contract.Requires(context != null);
            var scope = new LuaScope(context);
            scope.labels.Add(ReturnLabelName, Expr.Label(typeof(object)));
            return scope;
        }

        public static LuaScope CreateChildFrom(LuaScope parent)
        {
            var scope = new LuaScope(parent);
            LabelTarget breakLabel;
            if (parent.labels.TryGetValue(BreakLabelName, out breakLabel))
                scope.labels.Add(BreakLabelName, breakLabel);
            return scope;
        }

        public static LuaScope CreateFunctionChildFrom(LuaScope parent)
        {
            var scope = new LuaScope(parent);
            scope.labels.Add(ReturnLabelName, Expr.Label(typeof(object)));
            return scope;
        }

        public LuaScope GetParent()
        {
            return parent;
        }

        public LuaScope GetRoot()
        {
            var temp = this;
            while (temp.parent != null)
                temp = temp.parent;
            return temp;
        }
    }
}
