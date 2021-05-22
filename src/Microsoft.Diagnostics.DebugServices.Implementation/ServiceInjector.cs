// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Diagnostics.Contracts;

namespace Microsoft.Diagnostics.DebugServices.Implementation
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ServiceImportAttribute : Attribute
    {
        /// <summary>
        /// Marks this service import as required
        /// </summary>
        public bool Required { get; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceImportAttribute()
        {
        }
    }

    /// <summary>
    /// Service injector class to set properties, fields and methods marked 
    /// with the ServiceAttribute that match the provided services. Tracks 
    /// any unresolved service requests and injects them when the service
    /// is registered.
    /// </summary>
    public class ServiceInjector
    {
        private delegate bool CallbackFunc(object instance);

        private struct Callsite
        {
            public ServiceImportAttribute Attribute;
            public CallbackFunc Callback;
        }

        private readonly IServiceProvider _provider;
        private readonly Dictionary<Type, List<Callsite>> _callsites;

        /// <summary>
        /// Create a service injector instance
        /// </summary>
        public ServiceInjector(IServiceProvider serviceProvider)
        {
            _provider = serviceProvider;
            _callsites = new Dictionary<Type, List<Callsite>>();
        }

        /// <summary>
        /// Creates an instance of the type. Does not bind any services except
        /// what is passed to the constructor.
        /// </summary>
        /// <typeparam name="T">type to create</typeparam>
        /// <param name="parameters">other services or contexts to bind</param>
        /// <returns>object instance</returns>
        public T CreateInstance<T>(params object[] parameters)
        {
            return (T)CreateInstance(typeof(T), bind: false, parameters);
        }

        /// <summary>
        /// Creates an instance of the type and injects the services requested 
        /// on fields, properties or methods with the ServiceAttribute if bind
        /// is true.
        /// </summary>
        /// <typeparam name="T">type to create</typeparam>
        /// <param name="bind">if true bind the services requested</param>
        /// <param name="parameters">other services or contexts to bind</param>
        /// <returns>object instance</returns>
        public T CreateInstance<T>(bool bind, params object[] parameters)
        {
            return (T)CreateInstance(typeof(T), bind, parameters);
        }

        /// <summary>
        /// Creates an instance of the type and injects the services requested 
        /// on fields, properties or methods with the ServiceAttribute if bind
        /// is true.
        /// </summary>
        /// <param name="type">service or context type</param>
        /// <param name="bind">if true bind the services requested</param>
        /// <param name="parameters">other services or contexts to bind</param>
        /// <returns>object instance</returns>
        public object CreateInstance(Type type, bool bind = false, params object[] parameters)
        {
            var provider = new ServiceProvider(_provider);
            foreach (object parameter in parameters) {
                // Add all the base types except object or value type
                for (Type parameterType = parameter.GetType(); parameterType != null; parameterType = parameterType.BaseType) {
                    if (parameterType == typeof(Object) || parameterType == typeof(ValueType)) {
                        break;
                    }
                    provider.AddService(parameterType, parameter);
                }
            }
            object instance = InvokeConstructor(type, provider);
            if (bind) {
                BindServices(instance, provider);
            }
            return instance;
        }

        /// <summary>
        /// Bind the fields, properties and methods with the service for it's type.
        /// </summary>
        /// <param name="instance">object to bind</param>
        /// <param name="provider">services</param>
        public void BindServices(object instance, IServiceProvider provider = null) 
        {
            Contract.Requires(instance != null);

            Dictionary<Type, List<Callsite>> callsites = BuildCallsites(instance, provider ?? _provider);

            foreach (KeyValuePair<Type, List<Callsite>> entry in callsites) {
                object service = (provider ?? _provider).GetService(entry.Key);
                if (service != null) {
                    foreach (Callsite callsite in entry.Value) {
                        bool collected = callsite.Callback(service); 
                        Contract.Requires(!collected); 
                    }
                }
                else if (_callsites == null) {
                    foreach (Callsite callsite in entry.Value) {
                        if (callsite.Attribute.Required) {
                            throw new DiagnosticsException($"{entry.Key.Name} service not found");
                        }
                    }
                }

                TrackCallsite(entry);
            }
        }

        /// <summary>
        /// Create the services in the specified assembly.
        /// </summary>
        /// <param name="assembly">assembly to look for ServiceAttribute on types</param>
        public void CreateServices(Assembly assembly)
        {
            foreach (Type type in assembly.DefinedTypes) {
                for (Type currentType = type; currentType != null; currentType = currentType.BaseType) {
                    if (currentType == typeof(Object) || currentType == typeof(ValueType)) {
                        break;
                    }
                    ServiceAttribute attribute = type.GetCustomAttribute<ServiceAttribute>(inherit: false);
                    if (attribute != null) {
                        object instance = CreateInstance(currentType, bind: false);
                    }
                }
            }
        }

        private static Dictionary<Type, List<Callsite>> BuildCallsites(object instance, IServiceProvider provider) 
        {
            var callsites = new Dictionary<Type, List<Callsite>>();
            var weakReference = new WeakReference(instance);
            Type serviceType = instance.GetType();

            for (Type currentType = serviceType; currentType != null; currentType = currentType.BaseType) {
                if (currentType == typeof(Object) || currentType == typeof(ValueType)) {
                    break;
                }
                FieldInfo[] fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in fields) {
                    if (field.IsLiteral) {
                        continue;
                    }
                    ServiceImportAttribute attribute = field.GetCustomAttribute<ServiceImportAttribute>(inherit: false);
                    if (attribute != null) {
                        bool callback(object service)
                        {
                            object obj = weakReference.Target;
                            if (obj != null) {
                                try {
                                    field.SetValue(obj, service);
                                }
                                catch (TargetInvocationException ex) {
                                    throw ex.InnerException;
                                }
                                return false;
                            }
                            return true;
                        }
                        List<Callsite> callbacks = callsites.GetAddValue(field.FieldType, () => new List<Callsite>());
                        callbacks.Add(new Callsite { Attribute = attribute, Callback = callback });
                    }
                }
                PropertyInfo[] properties = currentType.GetProperties(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (PropertyInfo property in properties) {
                    ServiceImportAttribute attribute = property.GetCustomAttribute<ServiceImportAttribute>(inherit: false);
                    if (attribute != null) {
                        bool callback(object service)
                        { 
                            object obj = weakReference.Target;
                            if (obj != null) {
                                try {
                                    property.SetValue(obj, service);
                                }
                                catch (TargetInvocationException ex) {
                                    throw ex.InnerException;
                                }
                                return false;
                            }
                            return true;
                        }
                        List<Callsite> callbacks = callsites.GetAddValue(property.PropertyType, () => new List<Callsite>());
                        callbacks.Add(new Callsite { Attribute = attribute, Callback = callback });
                    }
                }
                MethodInfo[] methods = currentType.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (MethodInfo method in methods) {
                    ServiceImportAttribute attribute = method.GetCustomAttribute<ServiceImportAttribute>(inherit: false);
                    if (attribute != null) {
                        bool callback(object service)
                        {
                            object obj = weakReference.Target;
                            if (obj != null) {
                                Invoke(method, obj, provider);
                                return false;
                            }
                            return true;
                        }
                        foreach (ParameterInfo parameter in method.GetParameters()) {
                            List<Callsite> callbacks = callsites.GetAddValue(parameter.ParameterType, () => new List<Callsite>());
                            callbacks.Add(new Callsite { Attribute = attribute, Callback = callback });
                        }
                    }
                }
            }

            return callsites;
        }

        /// <summary>
        /// Track the callsite (fields, properties or methods marked with service attribute) if not required
        /// </summary>
        /// <param name="entry">type/list of callbacks</param>
        private void TrackCallsite(KeyValuePair<Type, List<Callsite>> entry)
        {
            if (_callsites != null) { 
                List<Callsite> callsites = _callsites.GetAddValue(entry.Key, () => new List<Callsite>());
                callsites.AddRange(entry.Value);
            }
        }

        /// <summary>
        /// Binds all the callsite that have been found in BuildCallsites.
        /// </summary>
        /// <param name="type">service type</param>
        /// <param name="service">service instance</param>
        private void BindCallsites(Type type, object service)
        {
            if (_callsites != null) {
                if (_callsites.TryGetValue(type, out List<Callsite> callsites)) {
                    foreach (Callsite callsite in callsites.ToList()) {
                        if (callsite.Callback(service)) {
                            callsites.Remove(callsite);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Call the constructor of the type and return the instance binding any
        /// services in the constructor parameters.
        /// </summary>
        /// <param name="type">type to create</param>
        /// <param name="provider">services</param>
        /// <returns>type instance</returns>
        private static object InvokeConstructor(Type type, IServiceProvider provider)
        {
            ConstructorInfo constructor = type.GetConstructors().Single();
            object[] arguments = BuildArguments(constructor, provider);
            try {
                return constructor.Invoke(arguments);
            }
            catch (TargetInvocationException ex) {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Call the method and bind any services in the constructor parameters.
        /// </summary>
        /// <param name="method">method to invoke</param>
        /// <param name="instance">class instance</param>
        /// <param name="provider">services</param>
        /// <returns>method return value</returns>
        private static object Invoke(MethodInfo method, object instance, IServiceProvider provider)
        {
            object[] arguments = BuildArguments(method, provider);
            try {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException ex) {
                throw ex.InnerException;
            }
        }

        private static object[] BuildArguments(MethodBase methodBase, IServiceProvider provider)
        {
            ParameterInfo[] parameters = methodBase.GetParameters();
            object[] arguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                arguments[i] = provider.GetService(parameters[i].ParameterType);
            }
            return arguments;
        }
    }

    /// <summary>
    /// A set of helpers operating on various collection types.
    /// </summary>
    public static class DictionaryExtensions 
    {
        public static TValue GetAddValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> callback)
        {
            if (dictionary.TryGetValue(key, out TValue existingValue)) {
                return existingValue;
            }
            TValue newValue = callback();
            dictionary.Add(key, newValue);
            return newValue;
        }
    }
}
