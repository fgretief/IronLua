using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Actions;
using IronLua.Compiler;

namespace IronLua.Runtime
{
    internal sealed partial class CodeContext
    {
        public CodeContext(LuaContext language)
        {
            Language = language;
        }

        /// <summary>
        /// Gets the language context which is handling this code context
        /// </summary>
        public LuaContext Language
        { get; private set; }

        /// <summary>
        /// Gets the currently executing scope (if one is executing, otherwise null)
        /// </summary>
        public Scope ExecutingScope
        { get; private set; }

        /// <summary>
        /// Gets the currently executing scope's storage (if one is executing, otherwise null)
        /// </summary>
        public IDynamicMetaObjectProvider ExecutingScopeStorage
        { get; private set; }

        public DefaultBinder Binder
        { get { return Language.Binder; } }


        #region Execution Environment

        //Stores Function => Environment mappings to support custom environments for function execution
        private readonly Dictionary<LuaScope, LuaTable> _EnvironmentMappings = new Dictionary<LuaScope, LuaTable>();
        
        /// <summary>
        /// Determines whether or not the given function has a custom execution environment
        /// set for it.
        /// </summary>
        public bool HasCustomEnvironment(LuaScope function)
        {
            return _EnvironmentMappings.ContainsKey(function);
        }

        /// <summary>
        /// Gets the custom execution environment for the given function, or null if no environment was set
        /// </summary>
        public LuaTable GetFunctionEnvironment(LuaScope function)
        {
            if (!HasCustomEnvironment(function))
                return null;
            return _EnvironmentMappings[function];
        }
        
        #endregion

        #region Metatable management

        readonly Dictionary<Type, LuaTable> _metatables;

        Dictionary<Type, LuaTable> SetupMetatables()
        {
            return new Dictionary<Type, LuaTable>()
            {
                {typeof(bool), new LuaTable(this)},
                {typeof(double), new LuaTable(this)},
                {typeof(string), new LuaTable(this)},
                {typeof(Delegate), new LuaTable(this)},
            };
        }

        internal LuaTable GetTypeMetatable(object obj)
        {
            if (obj == null)
                return null;

            LuaTable table;

            if (obj is BoundMemberTracker)
            {
                var tracker = obj as BoundMemberTracker;

                if (tracker.ObjectInstance is LuaTable)
                {
                    if ((tracker.ObjectInstance as LuaTable).Metatable != null)
                        return (tracker.ObjectInstance as LuaTable).Metatable;
                }

                if (_metatables.TryGetValue(tracker.ObjectInstance.GetType(), out table))
                    return table;
            }

            var objType = obj.GetType();

            if (_metatables.TryGetValue(objType, out table))
                return table;

            throw new LuaRuntimeException(this, "Could not find metatable for '{0}'", objType.FullName);
        }

        internal LuaTable SetTypeMetatable(Type type, LuaTable metatable)
        {
            if (type == null || metatable == null)
                return null;

            LuaTable table;
            if (_metatables.TryGetValue(type, out table))
                return table;

            _metatables.Add(type, metatable);
            return metatable;
        }

        #endregion

    }
}
