using IronLua.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Scripting.Actions;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Collections;

namespace IronLua.Library
{
    class InteropLibrary : Library
    {
        public InteropLibrary(CodeContext context, params Type[] types)
            : base(context)
        {
            InteropMetatable = GenerateMetatable(context);
        }

        public override void Setup(IDictionary<string, object> table)
        {
            table.AddOrSet("import", (Func<string,object[], LuaTable>)ImportType);
            table.AddOrSet("method", (Func<object, string, object>)InteropGetMethod);
            table.AddOrSet("call", (Func<object, string, object[], object>)InteropCallMethod);
            table.AddOrSet("setvalue", (Func<object, string, object, object>)InteropSetValue);
            table.AddOrSet("getvalue", (Func<object, string, object>)InteropGetValue);
            table.AddOrSet("subscribe", (Action<object, string, Delegate>)InteropSubscribeEvent);
            table.AddOrSet("unsubscribe", (Action<object, string, Delegate>)InteropUnsubscribeEvent);
            table.AddOrSet("makearray", (Func<object, object, object>)MakeArray);
            table.AddOrSet("iterate", (Func<object, object[], object>)InteropEnumerate);
        }

        #region Static Types

        private static LuaTable InteropMetatable;

        private LuaTable ImportType(string typeName, params object[] args)
        {
            var type = Type.GetType(typeName, false);

            bool genNamespaces = false;

            if (args != null && args.Length == 1 && args[0] is bool)
                genNamespaces = (bool)args[0];

            return ImportType(type, genNamespaces);
        }

        internal LuaTable GetTypeTable(Type type)
        {
            if (type != null)
            {
                var table = new LuaTable(Context);
                table.SetConstant("__clrtype", type);
                table.Metatable = InteropMetatable;

                return table;
            }
            return null;
        }

        public LuaTable ImportType(Type type, bool generateNamespaces)
        {
            if (generateNamespaces)
            {
                string[] typeNameParts = type.FullName.Split('.');
                string rootNamespace = typeNameParts.First();
                string typeName = typeNameParts.Last();
                typeNameParts = typeNameParts.Skip(1).Reverse().Skip(1).Reverse().ToArray();

                IDictionary<string,object> current = null;

                var storage = Context.EngineGlobals.Storage as IDictionary<string,object>;

                if (storage.ContainsKey(rootNamespace))
                    current = storage[rootNamespace] as IDictionary<string,object>;
                else
                {
                    current = new LuaTable(Context);
                    storage.AddOrSet(rootNamespace, current);
                }

                if (current == null)
                    throw LuaRuntimeException.Create(Context, "Another variable is obscuring the type's required namespace name ({0})", rootNamespace);


                string soFar = rootNamespace;

                foreach (var part in typeNameParts)
                {
                    if (current == null)
                        throw LuaRuntimeException.Create(Context, "Another variable is obscuring the type's required namespace name ({0}.{1})", soFar, part);

                    else if (!current.ContainsKey(part))
                    {
                        LuaTable newTable = new LuaTable(Context);
                        current.Add(part, newTable);
                        current = newTable;
                    }
                    else
                        current = current[part] as IDictionary<string,object>;

                    soFar += "." + part;
                }

                var typeTable = GetTypeTable(type);
                current.AddOrSet(typeName, typeTable);

                Context.Language.SetTypeMetatable(type, InteropMetatable);
                return typeTable;
            }

            Context.Language.SetTypeMetatable(type, InteropMetatable);
            return GetTypeTable(type);
        }

        internal LuaTable GenerateMetatable(CodeContext context)
        {
            LuaTable table = new LuaTable(context);
            table.SetConstant(Constant.INDEX_METAMETHOD, (Func<object, object, object>)InteropIndex);
            table.SetConstant(Constant.NEWINDEX_METAMETHOD, (Func<object, object, object, object>)InteropNewIndex);
            table.SetConstant(Constant.CALL_METAMETHOD, (Func<object, object[], object>)InteropCall);
            table.SetConstant(Constant.CONCAT_METAMETHOD, (Func<string, object, string>)Concat);
            table.SetConstant(Constant.TOSTRING_METAFIELD, (Func<object, string>)ToString);
            table.SetConstant(Constant.LENGTH_METAMETHOD, (Func<object, object>)InteropLength);

            return table;
        }

        private string Concat(string str, object table)
        {
            return str + ToString(table);
        }

        private string ToString(object table)
        {
            if (table is LuaTable)
                return "[CLASS] " + ((table as LuaTable).GetValue("__clrtype") as Type).FullName;
            else
                return table.ToString();
        }

        private object InteropIndex(object target, object index)
        {
            if (target is LuaTable)
            {
                var type = (target as LuaTable).GetValue("__clrtype") as Type;

                if (type.IsEnum)
                    try
                    {
                        return Enum.Parse(type, BaseLibrary.ToString(Context, index));
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context, ex.Message, ex);
                    }
                
                var members = type.GetMember(index.ToString(), BindingFlags.Static | BindingFlags.Public);

                if (members.Any() && members.All(x => x.MemberType == MemberTypes.Field))
                    try
                    {
                        return (members.First() as FieldInfo).GetValue(null);
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context, 
                            string.Format("could not get field value for '{0}' on '{1}'", index.ToString(), BaseLibrary.Type(type)), 
                            ex);
                    }
                else if (members.Any() && members.All(x => x.MemberType == MemberTypes.Property))
                    try
                    {
                        return (members.First() as PropertyInfo).GetValue(null, null);
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context,
                            string.Format("could not get property value for '{0}' on '{1}'", index.ToString(), BaseLibrary.Type(type)),
                            ex);
                    }
                else if (members.Any() && members.All(x => x.MemberType == MemberTypes.Method))
                    return new BoundMemberTracker(MemberTracker.FromMemberInfo(members.First()), target as LuaTable);
            }
            else if (target.GetType().IsArray)
            {
                return (target as Array).GetValue(Convert.ToInt32(index));
            }
            else if (target.GetType().GetMethods().Any(x => x.Name.Equals("get_Item") && ParamsMatch(x, new[] { index.GetType() }) > 0))
            {
                var type = target.GetType();
                var methods = target.GetType().GetMethods().Where(x => x.Name.Equals("get_Item") && ParamsMatch(x, new[] { index.GetType() }) > 0)
                    .OrderByDescending(x => ParamsMatch(x, new[] { index.GetType() }));

                var method = methods.First();
                try
                {
                    return method.Invoke(target, ParamsConvert(method, new[] { index }));
                }
                catch (Exception ex)
                {
                    throw LuaRuntimeException.Create(Context, ex.Message, ex);
                }
            }
            else
            {
                var type = target.GetType();
                var members = type.GetMember(index.ToString(), BindingFlags.Instance | BindingFlags.Public);

                if (members.Any() && members.All(x => x.MemberType == MemberTypes.Field))
                    try
                    {
                        return (members.First() as FieldInfo).GetValue(target);
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context,
                            string.Format("could not get field value for '{0}' on '{1}'", index.ToString(), BaseLibrary.ToString(Context, target)),
                            ex);
                    }
                else if (members.Any() && members.All(x => x.MemberType == MemberTypes.Property))
                    try
                    {
                        return (members.First() as PropertyInfo).GetValue(target, null);
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context,
                            string.Format("could not get property value for '{0}' on '{1}'", index.ToString(), BaseLibrary.ToString(Context, target)),
                            ex);
                    }

                else if (members.Any() && members.All(x => x.MemberType == MemberTypes.Method))
                    return new BoundMemberTracker(MemberTracker.FromMemberInfo(members.First()), target);
            }

            throw LuaRuntimeException.Create(Context, "Unable to find a method, field or property identified by '{0}'", index);
        }

        private object InteropNewIndex(object target, object index, object value)
        {
            if (target is LuaTable)
            {
                var type = (target as LuaTable).GetValue("__clrtype") as Type;
                var members = type.GetMember(index.ToString(), BindingFlags.Static | BindingFlags.Public);

                if (members.Any() && members.All(x => x.MemberType == MemberTypes.Field))
                {
                    try
                    {
                        (members.First() as FieldInfo).SetValue(null, value);
                        return value;
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context,
                            string.Format("could not set field value for '{0}' on '{1}'", index.ToString(), BaseLibrary.Type(type)),
                            ex);
                    }
                }
                else if (members.Any() && members.All(x => x.MemberType == MemberTypes.Property))
                {
                    try
                    {
                        (members.First() as PropertyInfo).SetValue(null, value, null);
                        return value;
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context,
                            string.Format("could not set property value for '{0}' on '{1}'", index.ToString(), BaseLibrary.Type(type)),
                            ex);
                    }
                }
            }
            else if (target.GetType().IsArray)
            {
                try
                {
                    (target as Array).SetValue(Convert.ChangeType(value, target.GetType().GetElementType()), Convert.ToInt32(index));
                    return value;
                }
                catch (Exception ex)
                {
                    throw LuaRuntimeException.Create(Context,
                        string.Format("could not set indexed value for index '{0}' on array of type '{1}'", index.ToString(), BaseLibrary.Type(target.GetType().GetElementType())),
                        ex);
                }
            }
            else if (target.GetType().GetMethods().Any(x => x.Name.Equals("set_Item") && ParamsMatch(x, new[] { index.GetType() }) > 0))
            {
                var type = target.GetType();
                var methods = target.GetType().GetMethods().Where(x => x.Name.Equals("set_Item") && ParamsMatch(x, new[] { index.GetType(), value.GetType() }) > 0)
                    .OrderByDescending(x => ParamsMatch(x, new[] { index.GetType(), value.GetType() }));

                var method = methods.First();
                try
                {
                    return method.Invoke(target, ParamsConvert(method, new[] { index, value }));
                }
                catch (Exception ex)
                {
                    throw LuaRuntimeException.Create(Context, 
                        string.Format("could not set indexed value for index '{0}' on '{1}'", index.ToString(), BaseLibrary.Type(target.GetType().GetElementType())),
                        ex);
                }
            }
            else
            {
                var type = target.GetType();
                var members = type.GetMember(index.ToString(), BindingFlags.Instance | BindingFlags.Public);

                if (members.Any() && members.All(x => x.MemberType == MemberTypes.Field))
                {
                    try
                    {
                        (members.First() as FieldInfo).SetValue(target, value);
                        return value;
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context,
                            string.Format("could not set field value for '{0}' on '{1}'", index.ToString(), BaseLibrary.ToString(Context, target)),
                            ex);
                    }
                }
                else if (members.Any() && members.All(x => x.MemberType == MemberTypes.Property))
                {
                    try
                    {
                        (members.First() as PropertyInfo).SetValue(target, value, null);
                        return value;
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context,
                            string.Format("could not set property value for '{0}' on '{1}'", index.ToString(), BaseLibrary.ToString(Context, target)),
                            ex);
                    }
                }
            }

            throw LuaRuntimeException.Create(Context, "Unable to find a field or property identified by '{0}'", index);
        }

        private object InteropLength(object target)
        {
            if (target is Array)
                return (double)(target as Array).Length;
            else if (target is LuaTable)
                throw LuaRuntimeException.Create(Context, "Cannot get the length of a System.Type object");

            throw LuaRuntimeException.Create(Context, "Cannot get the length of a {0} object", target.GetType().FullName);
        }

        /// <summary>
        /// Acts upon a call to the specified interop type object. Behaves like a constructor call
        /// </summary>
        /// <param name="target">The interop type object being called</param>
        /// <param name="parameters">The parameters passed to the constructor</param>
        /// <returns>Returns the new instance of the interop type</returns>
        private object InteropCall(object target, params object[] parameters)
        {
            Type targetType = target.GetType();

            //CLR class reference (static references)
            if (targetType == typeof(LuaTable))
            {
                var type = (target as LuaTable).GetValue("__clrtype") as Type;

                var args = parameters.ToArray();
                var argsTypes = args.Select(x => x.GetType()).ToArray();

                return Activator.CreateInstance(type, args);
            }

            //CLR instance reference
            else if (targetType == typeof(BoundMemberTracker))
            {
                var tracker = target as BoundMemberTracker;

                Type type = null;
                BindingFlags flags = BindingFlags.Public;
                object targetObject = null;

                if (tracker.ObjectInstance is LuaTable)
                {
                    type = (tracker.ObjectInstance as LuaTable).GetValue("__clrtype") as Type;
                    flags |= BindingFlags.Static;
                }
                else
                {
                    type = tracker.ObjectInstance.GetType();
                    targetObject = tracker.ObjectInstance;
                    flags |= BindingFlags.Instance;
                }

                var methodName = tracker.Name;
                var argsTypes = parameters.Select(x => x.GetType()).ToArray();
                 

                var methods = type.GetMethods(flags)
                                .Where(x => x.Name.Equals(methodName) && ParamsMatch(x, argsTypes) > 0)
                                .OrderByDescending(x => ParamsMatch(x, argsTypes)).ToArray();
                
                if (methods.Length < 1)
                    throw LuaRuntimeException.Create(Context, "Could not find the method '{0}' on {1}", methodName, type.FullName);

                var method = methods.First();

                try
                {
                    return method.Invoke(targetObject, ParamsConvert(method, parameters.ToArray()));
                }
                catch (Exception ex)
                {
                    throw LuaRuntimeException.Create(Context, ex.Message, ex);
                }
            }
            
            throw LuaRuntimeException.Create(Context, BaseLibrary.Type(targetType) + " cannot be called directly as a function. If you want to itterate through the collection, use clr.itterate instead");
        }

        private object InteropEnumerate(object target, params object[] parameters)
        {
            var targetType = target.GetType();

            if (targetType.GetInterfaces().Any(i => i == typeof(IEnumerable)) || targetType.IsArray)
            {
                var e = (target as IEnumerable ?? target as Array).GetEnumerator();

                if (parameters.Length > 1 && parameters[1] != null)
                    while (e.MoveNext() && !parameters[1].Equals(e.Current)) ;

                return new Varargs(new Func<object, object, object>(InteropIterator), e);
            }
            else
                throw LuaRuntimeException.Create(Context, BaseLibrary.Type(target) + " does not implement IEnumerable and cannot be enumerated");

            throw LuaRuntimeException.Create(Context, "bad arguments");
        }

        private object InteropIterator(object iterator, object state)
        {
            var e = iterator as IEnumerator;
            if (e.MoveNext())
                return e.Current;
            return null;
        }

        #endregion

        #region Object Instances

        #region Method Calls

        private object InteropCallMethod(object target, string methodName, params object[] parameters)
        {
            if (target is LuaTable)
            {
                var type = (target as LuaTable).GetValue("__clrtype") as Type;
                var paramTypes = parameters == null ? new Type[0] : parameters.Select(x => x.GetType()).ToArray();

                var methods = type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(x => x.Name.Equals(methodName))
                    .Where(x => ParamsMatch(x, paramTypes) > 0)
                    .OrderByDescending(x => ParamsMatch(x, paramTypes)).ToArray();
                if (methods.Length < 1)
                    throw LuaRuntimeException.Create(Context, "Could not find a method with the given parameters");
                
                var method = methods.First();

                try
                {
                    return method.Invoke(null, parameters == null ? new object[0] : ParamsConvert(method, parameters.ToArray()));
                }
                catch (Exception ex)
                {
                    throw LuaRuntimeException.Create(Context, ex.Message, ex);
                }
            }
            else
            {
                var type = target.GetType();
                var paramTypes = parameters == null ? new Type[0] : parameters.Select(x => x.GetType()).ToArray();

                var methods = type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(x => x.Name.Equals(methodName))
                    .Where(x => ParamsMatch(x, paramTypes) > 0)
                    .OrderByDescending(x => ParamsMatch(x, paramTypes)).ToArray();
                if (methods.Length < 1)
                    throw LuaRuntimeException.Create(Context, "Could not find a method with the given parameters");

                var method = methods.First();
                try
                { 
                    return method.Invoke(target, parameters == null ? new object[0] : ParamsConvert(method, parameters.ToArray()));
                }
                catch (Exception ex)
                {
                    throw LuaRuntimeException.Create(Context, ex.Message, ex);
                }
            }
        }

        private object InteropGetMethod(object target, string methodName)
        {
            if (target is LuaTable)
            {
                var type = (target as LuaTable).GetValue("__clrtype") as Type;
                var methods = type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(x => x.Name.Equals(methodName)).ToArray();
                if (methods.Length > 0)
                {
                    var methodTable = new LuaTable(Context);
                    methodTable.SetConstant("__target", null);
                    methodTable.SetConstant("__clrtype", type);
                    methodTable.SetConstant("__method", methodName);
                    foreach (var method in methods)
                        methodTable.SetConstant(method, method);
                    methodTable.Metatable = GenerateMethodMetaTable();

                    return methodTable;
                }

            }
            else
            {
                var type = target.GetType();

                var methods = type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(x => x.Name.Equals(methodName)).ToArray();
                if (methods.Length > 0)
                {
                    var methodTable = new LuaTable(Context);
                    methodTable.SetConstant("__target", target);
                    methodTable.SetConstant("__clrtype", type);
                    methodTable.SetConstant("__method", methodName);
                    foreach (var method in methods)
                        methodTable.SetConstant(method, method);
                    methodTable.Metatable = GenerateMethodMetaTable();

                    return methodTable;
                }
            }
            return null;
        }

        private int ParamsMatch(MethodInfo method, Type[] paramTypes)
        {
            int value = 0;

            var parameters = method.GetParameters();
            bool hasParams = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                //Test if there are too many required parameters
                if (i >= paramTypes.Length && !parameters[i].IsOptional)
                    return 0;

                //Is exactly what we're looking for
                if(paramTypes[i] == parameters[i].ParameterType)
                { value += 8; continue; }

                //Can be cast to what we're looking for
                if(parameters[i].ParameterType.IsAssignableFrom(paramTypes[i]))
                { value += 4; continue; }

                //We can convert it to what we're looking for
                if (Context.Binder.CanConvertFrom(paramTypes[i], parameters[i].ParameterType, true, Microsoft.Scripting.Actions.Calls.NarrowingLevel.All))
                { value += 2; continue; }

                //We can convert it to what we're looking for
                if (Context.Binder.CanConvertFrom(paramTypes[i], parameters[i].ParameterType, false, Microsoft.Scripting.Actions.Calls.NarrowingLevel.All))
                { value += 1; continue; }

                //Test if the parameter's types match or not
                if (!(hasParams = (i == parameters.Length - 1))) //This is in case we have a possible params argument
                    return 0;
            }

            //If we have checked everything, then it's fine
            if (!hasParams)
                return parameters.Count() == paramTypes.Length ? value + 1 : 0;

            //Check if we have any params args...
            var paramsType = parameters.Last().ParameterType;
            if (!paramsType.IsArray)
                return 0;

            var paramItemsType = paramsType.GetElementType();
            for (int i = parameters.Length - 1; i < paramTypes.Length; i++)
                //Make sure that all the params arguments are valid
                if (!paramItemsType.IsAssignableFrom(paramTypes[i]))
                    return 0;

            value += 4;

            return value;
        }
        
        private object[] ParamsConvert(MethodInfo method, object[] parameters)
        {
            var methodParams = method.GetParameters();

            var output = new LinkedList<object>();

            for (int i = 0; i < parameters.Length; i++)
            {
                if (methodParams[i].ParameterType == parameters[i].GetType())
                    output.AddLast(parameters[i]);
                else
                    output.AddLast(Context.Binder.Convert(parameters[i], methodParams[i].ParameterType));
            }

            return output.ToArray();
        }

        private LuaTable GenerateMethodMetaTable()
        {
            var table = new LuaTable(Context);
            table.SetConstant(Constant.CALL_METAMETHOD, (Func<object, Varargs, object>)MethodInteropCall);
            table.SetConstant(Constant.TOSTRING_METAFIELD, (Func<LuaTable, string>)MethodTableToString);
            return table;
        }

        private string MethodTableToString(LuaTable table)
        {
            return string.Format("{0}.{1}(...)", (table.GetValue("__clrtype") as Type).FullName, table.GetValue("__method"));
        }

        private object MethodInteropCall(object target, Varargs parameters)
        {
            var table = target as LuaTable;

            var paramTypes = parameters.Select(x => x.GetType()).ToArray();

            Varargs pair = table.Next();
            do
            {
                var methodInfo = pair[0] as MethodInfo;
                if (methodInfo == null)
                    continue;

                if (ParamsMatch(methodInfo, paramTypes) > 0)
                    try
                    {
                        return methodInfo.Invoke(table.GetValue("__target"), parameters.ToArray());
                    }
                    catch (Exception ex)
                    {
                        throw LuaRuntimeException.Create(Context, ex.Message, ex);
                    }

            } while ((pair = table.Next(pair[0])) != null);

            throw LuaRuntimeException.Create(Context, "Could not find a method with the given parameters");
        }

        #endregion

        #region Get/Set Value

        private object InteropGetValue(object table, string propertyName)
        {
            //Static calls
            if (table is LuaTable)
            {
                var type = (table as LuaTable).GetValue("__clrtype") as Type;

                var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
                if (property != null)
                    return property.GetValue(null, null);

                var field = type.GetField(propertyName, BindingFlags.Static | BindingFlags.Public);
                if (field != null)
                    return field.GetValue(null);

                throw LuaRuntimeException.Create(Context, "The static field or property '{0}' was not found", propertyName);
            }

            //Instance calls
            else
            {
                var type = table.GetType();

                var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property != null)
                    return property.GetValue(table, null);

                var field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (field != null)
                    return field.GetValue(table);

                throw LuaRuntimeException.Create(Context, "The instance field or property '{0}' was not found", propertyName);
            }
        }
        
        private object InteropSetValue(object table, string propertyName, object value)
        {
            //Static calls
            if (table is LuaTable)
            {
                var type = (table as LuaTable).GetValue("__clrtype") as Type;

                var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
                if (property != null)
                {
                    property.SetValue(null, value, null);
                    return value;
                }

                var field = type.GetField(propertyName, BindingFlags.Static | BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(null, value);
                    return value;
                }

                throw LuaRuntimeException.Create(Context, "The static field or property '{0}' was not found", propertyName);
            }

            //Instance calls
            else
            {
                var type = table.GetType();

                var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property != null)
                {
                    property.SetValue(table, value, null);
                    return value;
                }

                var field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(table, value);
                    return value;
                }

                throw LuaRuntimeException.Create(Context, "The static field or property '{0}' was not found", propertyName);
            }
        }
        
        #endregion

        #region Event Handlers

        private void InteropSubscribeEvent(object target, string eventName, Delegate handler)
        {            
            //Static events
            if (target is LuaTable)
            {
                var type = (target as LuaTable).GetValue("__clrtype") as Type;

                var eventSource = type.GetEvent(eventName, BindingFlags.Static | BindingFlags.Public);
                
                if (eventSource != null)
                {
                    Delegate safeHandler = GetEventHandlerDelegate(eventSource.EventHandlerType, handler);
                    eventSource.AddEventHandler(null, safeHandler);
                    return;
                }

                throw LuaRuntimeException.Create(Context, "The static event '{0}' was not found", eventName);
            }

            //Instance events
            else
            {
                var type = target.GetType();

                var eventSource = type.GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                if (eventSource != null)
                {
                    Delegate safeHandler = GetEventHandlerDelegate(eventSource.EventHandlerType, handler);
                    eventSource.AddEventHandler(target, safeHandler);
                    return;
                }

                throw LuaRuntimeException.Create(Context, "The instance event '{0}' was not found", eventName);
            }
        }

        private void InteropUnsubscribeEvent(object target, string eventName, Delegate handler)
        {
            //Static events
            if (target is LuaTable)
            {
                var type = (target as LuaTable).GetValue("__clrtype") as Type;

                var eventSource = type.GetEvent(eventName, BindingFlags.Static | BindingFlags.Public);
                if (eventSource != null)
                {
                    Delegate safeHandler = GetEventHandlerDelegate(eventSource.EventHandlerType, handler);
                    eventSource.AddEventHandler(null, safeHandler);
                    return;
                }

                throw LuaRuntimeException.Create(Context, "The static event '{0}' was not found", eventName);
            }

            //Instance events
            else
            {
                var type = target.GetType();

                var eventSource = type.GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                if (eventSource != null)
                {
                    Delegate safeHandler = GetEventHandlerDelegate(eventSource.EventHandlerType, handler);
                    eventSource.AddEventHandler(target, safeHandler);
                    return;
                }

                throw LuaRuntimeException.Create(Context, "The instance event '{0}' was not found", eventName);
            }
        }

        private Delegate GetEventHandlerDelegate(Type eventType, Delegate handler)
        {
            if (eventType.IsInstanceOfType(handler))
                return handler;
            
            if (typeof(EventHandler).IsAssignableFrom(eventType))
                return new EventHandler((sender, e) => handler.DynamicInvoke(new[] { sender, e }));
            else if(eventType.IsGenericType && typeof(EventHandler<EventArgs>).GetGenericTypeDefinition().IsAssignableFrom(eventType.GetGenericTypeDefinition()))
                return Delegate.CreateDelegate(typeof(EventHandler<EventArgs>).GetGenericTypeDefinition()
                    .MakeGenericType(eventType.GetGenericArguments()), handler, 
                        this.GetType().GetMethod("InvokeEventHandler", BindingFlags.Static | BindingFlags.NonPublic));
            
            //Is this necessary? Maybe the above method can just be tweaked to work with other implementations as well?
            var parameters = handler.Method.GetParameters().Skip(1).Select(x => Expression.Parameter(x.ParameterType, x.Name)).ToArray();
            var newArray = Expression.NewArrayInit(typeof(object), parameters.Select(x => Expression.Convert(x, typeof(object))));
            var lambda = Expression.Lambda(Expression.GetActionType(parameters.Select(x => x.Type).ToArray()),
                    Expression.Block(parameters,
                    Expression.Call(Expression.Constant(handler), handler.GetType().GetMethod("DynamicInvoke",BindingFlags.Instance | BindingFlags.Public),
                        newArray
                    )
                    ), parameters
                ).Compile();

            return lambda;
        }

        private static void InvokeEventHandler(Delegate target, object sender, EventArgs e)
        {
            target.DynamicInvoke(new[] { sender, e });
        }

        #endregion

        #endregion

        #region Helper Functions

        private object MakeArray(object typeContainer, object length)
        {
            Type type = null;
            if (typeContainer is LuaTable)
                type = (typeContainer as LuaTable).GetValue("__clrtype") as Type;
            else if (typeContainer is Type)
                type = typeContainer as Type;
            else type = typeContainer.GetType();

            if (type == null)
                throw LuaRuntimeException.Create(Context, "Could not determine type of array to create");

            //Ensure that we have an array type for this object
            ImportType(type.MakeArrayType(), false);

            try
            {
                int len = (int)Convert.ToInt32(length);

                return Array.CreateInstance(type, len);
            }
            catch (Exception ex)
            {
                throw LuaRuntimeException.Create(Context, ex.Message, ex);
            }
        }

        #endregion
    }
}
