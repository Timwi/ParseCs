﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RT.ParseCs
{
    /// <summary>
    ///     Provides the means to resolve a name while compiling an expression to an expression tree (see <see
    ///     cref="CsExpression.ToLinqExpression"/>).</summary>
    public class NameResolver
    {
        private string _currentNamespace;
        private Type _currentType;
        private object _currentInstance;
        private Assembly[] _assemblies;
        private Dictionary<string, ResolveContext> _localNames = new Dictionary<string, ResolveContext>();
        private List<string> _usingNamespaces = new List<string>();

        private NameResolver(string currentNamespace, Type currentType, object currentInstance, Assembly[] assemblies)
        {
            _currentNamespace = currentNamespace;
            _currentType = currentType;
            _currentInstance = currentInstance;
            _assemblies = assemblies;
        }

        /// <summary>
        ///     Generates a <see cref="NameResolver"/> that resolves identifiers from the point of view of code inside a type.</summary>
        /// <param name="type">
        ///     The type according to whose context names are resolved.</param>
        /// <param name="assemblies">
        ///     A set of target assemblies from which to resolve type references.</param>
        /// <returns>
        ///     A name resolver.</returns>
        public static NameResolver FromType(Type type, params Assembly[] assemblies)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            return new NameResolver(type.Namespace, type, null, assemblies);
        }

        /// <summary>
        ///     Generates a <see cref="NameResolver"/> that resolves identifiers from the point of view of code inside an
        ///     instance method.</summary>
        /// <param name="instance">
        ///     An object instance. Names are resolved according to the context of any instance method within this object’s
        ///     type.</param>
        /// <param name="assemblies">
        ///     A set of target assemblies from which to resolve type references.</param>
        /// <returns>
        ///     A name resolver.</returns>
        public static NameResolver FromInstance(object instance, params Assembly[] assemblies)
        {
            if (instance == null)
                throw new ArgumentNullException("instance");
            var type = instance.GetType();
            return new NameResolver(type.Namespace, type, instance, assemblies);
        }

        internal ResolveContext ResolveSimpleName(CsIdentifier simpleName, ResolveContext context = null)
        {
            if (context is ResolveContextMethodGroup)
                throw new NotImplementedException("Access to method groups is not supported.");

            var simpleNameBuiltin = simpleName as CsKeywordIdentifier;
            if (simpleNameBuiltin != null)
            {
                if (context != null)
                    throw new InvalidOperationException("Unexpected built-in: {0}.".Fmt(simpleNameBuiltin.ToString()));

                switch (simpleNameBuiltin.Keyword)
                {
                    case "string": return new ResolveContextType(typeof(string));
                    case "sbyte": return new ResolveContextType(typeof(sbyte));
                    case "byte": return new ResolveContextType(typeof(byte));
                    case "short": return new ResolveContextType(typeof(short));
                    case "ushort": return new ResolveContextType(typeof(ushort));
                    case "int": return new ResolveContextType(typeof(int));
                    case "uint": return new ResolveContextType(typeof(uint));
                    case "long": return new ResolveContextType(typeof(long));
                    case "ulong": return new ResolveContextType(typeof(ulong));
                    case "object": return new ResolveContextType(typeof(object));
                    case "bool": return new ResolveContextType(typeof(bool));
                    case "char": return new ResolveContextType(typeof(char));
                    case "float": return new ResolveContextType(typeof(float));
                    case "double": return new ResolveContextType(typeof(double));
                    case "decimal": return new ResolveContextType(typeof(decimal));

                    case "this":
                        if (_currentInstance == null)
                            throw new InvalidOperationException("Cannot use ‘this’ pointer when no current instance exists.");
                        return new ResolveContextConstant(_currentInstance);

                    case "base":
                        throw new InvalidOperationException("Base method calls are not supported.");

                    default:
                        throw new InvalidOperationException("Unexpected built-in: {0}.".Fmt(simpleNameBuiltin.ToString()));
                }
            }

            var simpleNameIdentifier = simpleName as CsNameIdentifier;
            if (simpleNameIdentifier == null)
                throw new InvalidOperationException("Unexpected simple-name type: {0}.".Fmt(simpleName.GetType().FullName));

            // Is it a local?
            if (context == null && _localNames.ContainsKey(simpleNameIdentifier.Name))
                return _localNames[simpleNameIdentifier.Name];

            List<string> candidatePrefixes = new List<string>();

            if (context is ResolveContextNamespace)
                candidatePrefixes.Add(((ResolveContextNamespace) context).Namespace);
            else if ((context == null && _currentNamespace == null) || context is ResolveContextGlobal)
                candidatePrefixes.Add(null);
            else if (context == null)
            {
                candidatePrefixes.Add(null);
                string soFar = null;
                foreach (var part in _currentNamespace.Split('.'))
                {
                    soFar += (soFar == null ? "" : ".") + part;
                    candidatePrefixes.Add(soFar);
                }
                candidatePrefixes.AddRange(_usingNamespaces);
            }

            // Is it a namespace?
            if (simpleNameIdentifier.GenericTypeArguments == null && (context == null || context is ResolveContextNamespace) && _assemblies != null)
            {
                foreach (var prefix in candidatePrefixes)
                {
                    var prefixWithName = prefix == null ? simpleNameIdentifier.Name : prefix + "." + simpleNameIdentifier.Name;
                    var typeWithNamespace = _assemblies.SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Namespace == prefixWithName || (t.Namespace != null && t.Namespace.StartsWith(prefixWithName + ".")));
                    if (typeWithNamespace != null)
                        return new ResolveContextNamespace(prefixWithName);
                }
            }

            // Custom resolver
            ICustomResolver icr;
            ResolveContextConstant rcc;
            if ((context == null && (icr = _currentInstance as ICustomResolver) != null) || ((rcc = context as ResolveContextConstant) != null && (icr = rcc.Constant as ICustomResolver) != null))
                return new ResolveContextConstant(icr.Resolve(simpleNameIdentifier.Name));

            // Is it a type?
            if (context == null || context is ResolveContextNamespace || context is ResolveContextType)
            {
                IEnumerable<Type> searchTypes;
                if (context is ResolveContextType)
                    searchTypes = ((ResolveContextType) context).Type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                else
                    searchTypes = candidatePrefixes.SelectMany(prf => _assemblies.SelectMany(a => a.GetTypes()).Where(t => t.Namespace == prf));
                foreach (var type in searchTypes.Where(t => t.Name == simpleNameIdentifier.Name))
                {
                    if (simpleNameIdentifier.GenericTypeArguments == null && !type.IsGenericType)
                        return new ResolveContextType(type);

                    if (simpleNameIdentifier.GenericTypeArguments != null && type.IsGenericType && type.GetGenericArguments().Length == simpleNameIdentifier.GenericTypeArguments.Count)
                        return new ResolveContextType(type.MakeGenericType(simpleNameIdentifier.GenericTypeArguments.Select(tn => ResolveType(tn)).ToArray()));
                }
            }

            Type typeInContext = context == null ? _currentType : context.ExpressionType;

            if (typeInContext != null)
            {
                var parent = context ?? (_currentInstance != null ? (ResolveContext) new ResolveContextConstant(_currentInstance) : new ResolveContextType(_currentType));
                if (simpleNameIdentifier.GenericTypeArguments == null)
                {
                    bool expectStatic = (context == null && _currentInstance == null) || (context is ResolveContextType);

                    // Is it a field or an event? (GetFields() finds the backing field of the event, which has the same name as the event)
                    foreach (var field in typeInContext.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Where(f => f.Name == simpleNameIdentifier.Name))
                    {
                        if (field.IsStatic != expectStatic)
                            throw new InvalidOperationException("Cannot access instance field through type name or static field through instance.");
                        return new ResolveContextExpression(Expression.Field(field.IsStatic ? null : parent.ToExpression(), field));
                    }

                    // Is it a non-indexed property?
                    foreach (var property in typeInContext.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Where(p => p.Name == simpleNameIdentifier.Name && p.GetIndexParameters().Length == 0))
                    {
                        bool isStatic = property.GetGetMethod() != null ? property.GetGetMethod().IsStatic : property.GetSetMethod().IsStatic;
                        if (isStatic != expectStatic)
                            throw new InvalidOperationException("Cannot access instance property through type name or static property through instance.");
                        return new ResolveContextExpression(Expression.Property(isStatic ? null : parent.ToExpression(), property));
                    }
                }

                // Is it a method group?
                var methodList = new List<MethodGroupMember>();
                foreach (var method in typeInContext.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Where(m => m.Name == simpleNameIdentifier.Name))
                {
                    if (simpleNameIdentifier.GenericTypeArguments == null)
                        methodList.Add(new MethodGroupMember(method, isExtensionMethod: false));
                    else if (simpleNameIdentifier.GenericTypeArguments != null && method.IsGenericMethod && method.GetGenericArguments().Length == simpleNameIdentifier.GenericTypeArguments.Count)
                        methodList.Add(new MethodGroupMember(method.MakeGenericMethod(simpleNameIdentifier.GenericTypeArguments.Select(tn => ResolveType(tn)).ToArray()), isExtensionMethod: false));
                }

                // Is it an extension method?
                foreach (var method in _assemblies.SelectMany(a => a.GetTypes()).SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)).Where(m => m.Name == simpleNameIdentifier.Name && m.IsDefined(typeof(ExtensionAttribute), false)))
                {
                    var prms = method.GetParameters();
                    if (prms.Length == 0)
                        continue;

                    if (simpleNameIdentifier.GenericTypeArguments == null)
                        methodList.Add(new MethodGroupMember(method, isExtensionMethod: true));
                    else if (simpleNameIdentifier.GenericTypeArguments != null && method.IsGenericMethod && method.GetGenericArguments().Length == simpleNameIdentifier.GenericTypeArguments.Count)
                        methodList.Add(new MethodGroupMember(method.MakeGenericMethod(simpleNameIdentifier.GenericTypeArguments.Select(tn => ResolveType(tn)).ToArray()), isExtensionMethod: true));
                }
                if (methodList.Count > 0)
                    return new ResolveContextMethodGroup(parent, methodList, simpleNameIdentifier.Name);
            }

            throw new InvalidOperationException("The name “{0}” could not be resolved.".Fmt(simpleName));
        }

        /// <summary>
        ///     Returns the type corresponding to the specified parsed type name.</summary>
        /// <param name="typeName">
        ///     The parse tree node representing the type identifier.</param>
        /// <returns>
        ///     The resolved type.</returns>
        public Type ResolveType(CsTypeName typeName)
        {
            if (typeName is CsEmptyGenericParameter)
                throw new InvalidOperationException("CsEmptyGenericParameter not allowed here.");

            if (typeName is CsConcreteTypeName)
            {
                var concrete = (CsConcreteTypeName) typeName;
                var elem = ResolveSimpleName(concrete.Parts[0], concrete.HasGlobal ? new ResolveContextGlobal() : null);
                foreach (var part in concrete.Parts.Skip(1))
                    elem = ResolveSimpleName(part, elem);
                if (!(elem is ResolveContextType))
                    throw new InvalidOperationException("“{0}” is not a type.".Fmt(typeName.ToString()));
                return ((ResolveContextType) elem).Type;
            }

            if (typeName is CsArrayTypeName)
                return ((CsArrayTypeName) typeName).ArrayRanks.Aggregate(ResolveType(((CsArrayTypeName) typeName).InnerType), (type, rank) => rank == 1 ? type.MakeArrayType() : type.MakeArrayType(rank));

            if (typeName is CsPointerTypeName)
                return ResolveType(((CsPointerTypeName) typeName).InnerType).MakePointerType();

            if (typeName is CsNullableTypeName)
                return typeof(Nullable<>).MakeGenericType(ResolveType(((CsNullableTypeName) typeName).InnerType));

            throw new NotImplementedException();
        }

        /// <summary>
        ///     Adds a local name to this name resolver.</summary>
        /// <param name="name">
        ///     Specifies the identifier.</param>
        /// <param name="resolveToExpression">
        ///     The expression to which the specified identifier should resolve.</param>
        public void AddLocalName(string name, Expression resolveToExpression)
        {
            if (_localNames.ContainsKey(name))
                throw new InvalidOperationException("The variable “{0}” cannot be redeclared because it is already used (perhaps in a parent scope) to denote something else.".Fmt(name));
            _localNames[name] = new ResolveContextExpression(resolveToExpression);
        }

        /// <summary>
        ///     Adds a local name to this name resolver.</summary>
        /// <param name="name">
        ///     Specifies the identifier.</param>
        /// <param name="resolveToType">
        ///     The type to which the specified identifier should resolve.</param>
        public void AddLocalName(string name, Type resolveToType)
        {
            if (_localNames.ContainsKey(name))
                throw new InvalidOperationException("The type “{0}” cannot be redeclared because it is already used to denote something else.".Fmt(name));
            _localNames[name] = new ResolveContextType(resolveToType);
        }

        /// <summary>
        ///     Removes a name from this name resolver.</summary>
        /// <param name="name">
        ///     The name to forget.</param>
        /// <remarks>
        ///     If <paramref name="name"/> is also the name of a namespace in any of the target assemblies, the name will
        ///     still resolve to that namespace. This method only removes local names added via <see
        ///     cref="AddLocalName(string,Expression)"/> or <see cref="AddLocalName(string,Type)"/>.</remarks>
        public void ForgetLocalName(string name)
        {
            _localNames.Remove(name);
        }

        /// <summary>
        ///     Adds the equivalent of a <c>using</c> declaration.</summary>
        /// <param name="namespace">
        ///     The namespace specified in the <c>using</c> declaration.</param>
        /// <example>
        ///     <para>
        ///         If the <paramref name="namespace"/> is equal to <c>System</c> and one of the target assemblies contains a
        ///         type called <c>System.DateTime</c>, the name <c>DateTime</c> will resolve to that type.</para></example>
        public void AddUsing(string @namespace)
        {
            _usingNamespaces.Add(@namespace);
        }
    }

    /// <summary>Provides the means to resolve names to specific object instances.</summary>
    public interface ICustomResolver
    {
        /// <summary>
        ///     When implemented in a derived class, returns the result of resolving the specified <paramref name="name"/>.</summary>
        /// <param name="name">
        ///     The name to resolve.</param>
        /// <returns>
        ///     The object instance the name resolves to.</returns>
        object Resolve(string name);
    }
}
