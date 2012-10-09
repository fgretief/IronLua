using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Threading;
using Expr = System.Linq.Expressions.Expression;
using ParamExpr = System.Linq.Expressions.ParameterExpression;
using IronLua.Runtime;
using System.Dynamic;
using Microsoft.Scripting.Ast;
using System.Linq;
using Microsoft.Scripting.Utils;

namespace IronLua.Compiler
{
    class LuaScope
    {
        const string BreakLabelName = "@break";
        const string ReturnLabelName = "@return";



        static readonly ParamExpr _DlrGlobals = Expr.Parameter(typeof(IDynamicMetaObjectProvider), "_ENV");
        readonly LuaScope parent;
        readonly CodeContext context;
        //readonly Dictionary<string, ParamExpr> variables;
        readonly Dictionary<string, LabelTarget> labels;
        readonly LambdaBuilder builder;
        
        static int hiddenId;

        public bool IsRoot { get { return parent == null; } }

        private LuaScope(CodeContext context, LambdaBuilder _builder)
        {
            this.context = context;
            this.parent = null;
            //this.variables = new Dictionary<string, ParamExpr>();
            builder = _builder;
            this.labels = new Dictionary<string, LabelTarget>();
        }

        private LuaScope(LuaScope parent, LambdaBuilder _builder, bool hiddenScope)
        {
            this.context = parent.context;
            this.parent = parent;
            //this.variables = new Dictionary<string, ParamExpr>();
            builder = _builder;
            builder.Visible = !hiddenScope;
            this.labels = new Dictionary<string, LabelTarget>();
        }

        public int LocalsCount
        {
            get { return builder.Locals.Count; }
        }

        public ParamExpr[] AllLocals()
        {
            return builder.Locals.ToArray();
        }

        public IEnumerable<string> GetLocalNames()
        {
            //return variables.Keys;
            return builder.Locals.Select(x => x.Name);
        }

        public IEnumerable<ParamExpr> GetLocals()
        {
            return builder.GetVisibleVariables();
            //return variables.Values;
        }

        public bool TryGetLocal(string name, out ParamExpr local)
        {
            if(name.Equals(Constant.VARARGS))
            {
                local = builder.ParamsArray;
                return local != null;
            }

            local = builder.Locals.Find(x => x.Name.Equals(name));

            if (builder.Locals.FindIndex(x => x.Name.Equals(name)) != -1)            
                return true;

            local = builder.Parameters.Find(x => x.Name.Equals(name));
            if (builder.Parameters.FindIndex(x => x.Name.Equals(name)) != -1)
                return true;

            if (parent != null)
                return parent.TryGetLocal(name, out local);

            return false;
        }

        public ParamExpr AddLocal(string name, Type type = null)
        {
            // We have this behavior so that ex. "local x, x = 1, 2" works

            //NOTE: builder.Variable returns a ParameterExpression (type casted to Expression); this needs to be fixed in the DLR
            return builder.Variable(type ?? typeof(object), name) as ParameterExpression;

            //ParamExpr param;
            //if (!variables.TryGetValue(name, out param))
            //    variables.Add(name, param = Expr.Variable(type ?? typeof(object), name));

            //return param;
        }

        public ParamExpr AddParameter(string name, Type type = null)
        {
            return builder.Parameter(type ?? typeof(object), name);
        }

        public ParamExpr AddVarargs()
        {
            return builder.CreateParamsArray(typeof(object), Constant.VARARGS);
        }

        public ParamExpr AddHidden(string name, Type type = null)
        {
            //var id = Interlocked.Increment(ref hiddenId);
            //var key = String.Format("$H{0}", id);
            //variables.Add(key, param);
            return builder.HiddenVariable(type ?? typeof(object), name);
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
            var scope = new LuaScope(context, Utils.Lambda(typeof(object), "(chunk)"));
            scope.labels.Add(ReturnLabelName, Expr.Label(typeof(object)));
            return scope;
        }

        public static LuaScope CreateChildFrom(LuaScope parent)
        {
            var scope = new LuaScope(parent, Utils.Lambda(typeof(object), "(block)"), true);
            LabelTarget breakLabel;
            if (parent.labels.TryGetValue(BreakLabelName, out breakLabel))
                scope.labels.Add(BreakLabelName, breakLabel);
            return scope;
        }

        public static LuaScope CreateFunctionChildFrom(LuaScope parent, string name)
        {
            var scope = new LuaScope(parent, Utils.Lambda(typeof(object), name), false);
            scope.labels.Add(ReturnLabelName, Expr.Label(typeof(object)));
            return scope;
        }

        public LuaScope GetParent()
        {
            return parent;
        }

        public CodeContext GetContext()
        {
            return context;
        }

        public LuaScope GetRoot()
        {
            var temp = this;
            while (temp.parent != null)
                temp = temp.parent;
            return temp;
        }

        public LambdaExpression ToLambda(Expr body)
        {
            ContractUtils.Requires(body != null);

            builder.Body = body;
            return builder.MakeLambda();
        }

        public Expression<T> ToLambda<T>(Expr body)
        {
            ContractUtils.Requires(body != null);

            builder.Body = body;
            return builder.MakeLambda(typeof(T)) as Expression<T>;
        }
    }
}
