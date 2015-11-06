using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RT.ParseCs
{
    /// <summary>Provides a base class for all nodes in the C# parse tree.</summary>
    public abstract class CsNode
    {
        /// <summary>The character index at which the node begins in the source code.</summary>
        public int StartIndex;
        /// <summary>The character index at which the node ends in the source code.</summary>
        public int EndIndex;

        /// <summary>Enumerates all the descendent nodes that are contained in this node.</summary>
        public virtual IEnumerable<CsNode> Subnodes
        {
            get
            {
                yield return this;

                var type = this.GetType();
                var subnodes = new List<CsNode>();
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (typeof(CsNode).IsAssignableFrom(field.FieldType))
                        subnodes.Add(field.GetValue(this) as CsNode);
                    else if (typeof(IEnumerable).IsAssignableFrom(field.FieldType))
                        subnodes.AddRange(recurse(field.GetValue(this) as IEnumerable).SelectMany(ienum => ienum.OfType<CsNode>()));
                }

                foreach (var subnode in subnodes)
                    if (subnode != null)
                        foreach (var node in subnode.Subnodes)
                            yield return node;
            }
        }

        private IEnumerable<IEnumerable> recurse(IEnumerable ienum)
        {
            if (ienum == null)
                yield break;
            yield return ienum;
            foreach (var subEnum in ienum.OfType<IEnumerable>().SelectMany(recurse))
                yield return subEnum;
        }
    }

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

    #region Document & Namespace
    /// <summary>C# document (source file).</summary>
    public sealed class CsDocument : CsNode
    {
        /// <summary>The <c>using</c> declarations that import a namespace.</summary>
        public List<CsUsingNamespace> UsingNamespaces = new List<CsUsingNamespace>();
        /// <summary>The <c>using</c> declarations that define an alias.</summary>
        public List<CsUsingAlias> UsingAliases = new List<CsUsingAlias>();
        /// <summary>The top-level custom attributes that are not attached to a type.</summary>
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        /// <summary>The namespaces declared in this file.</summary>
        public List<CsNamespace> Namespaces = new List<CsNamespace>();
        /// <summary>The types declared outside of any namespace in this file.</summary>
        public List<CsType> Types = new List<CsType>();

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var ns in UsingNamespaces)
                sb.Append(ns.ToString());
            if (UsingNamespaces.Any())
                sb.Append('\n');

            foreach (var alias in UsingAliases)
                sb.Append(alias.ToString());
            if (UsingAliases.Any())
                sb.Append('\n');

            foreach (var attr in CustomAttributes)
            {
                sb.Append(attr.ToString());
                sb.Append('\n');
            }
            if (CustomAttributes.Any())
                sb.Append('\n');

            bool first = true;
            foreach (var ns in Namespaces)
            {
                if (!first)
                    sb.Append("\n");
                first = false;
                sb.Append(ns.ToString());
            }
            foreach (var ty in Types)
            {
                if (!first)
                    sb.Append("\n");
                first = false;
                sb.Append(ty.ToString());
            }

            return sb.ToString();
        }
    }
    /// <summary>Base class for C# <c>using</c> declarations.</summary>
    public abstract class CsUsing : CsNode { }
    /// <summary>C# <c>using</c> declaration that imports a namespace (for example, <c>using System;</c>).</summary>
    public sealed class CsUsingNamespace : CsUsing
    {
        /// <summary>The namespace imported.</summary>
        public string Namespace;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "using " + Namespace.Sanitize() + ";\n"; }
    }
    /// <summary>C# <c>using</c> declaration that defines an alias (for example, <c>using M = System.Math;</c>).</summary>
    public sealed class CsUsingAlias : CsUsing
    {
        /// <summary>The alias defined (the name before the <c>=</c> sign).</summary>
        public string Alias;
        /// <summary>The type name the alias is defined to refer to (the part after the <c>=</c> sign).</summary>
        public CsTypeName Original;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "using " + Alias.Sanitize() + " = " + Original.ToString() + ";\n"; }
    }
    /// <summary>C# namespace declaration.</summary>
    public sealed class CsNamespace : CsNode
    {
        /// <summary>The name of the namespace.</summary>
        public string Name;
        /// <summary>The <c>using</c> declarations that import a namespace.</summary>
        public List<CsUsingNamespace> UsingNamespaces = new List<CsUsingNamespace>();
        /// <summary>The <c>using</c> declarations that define an alias.</summary>
        public List<CsUsingAlias> UsingAliases = new List<CsUsingAlias>();
        /// <summary>The namespaces declared nested inside this namespace.</summary>
        public List<CsNamespace> Namespaces = new List<CsNamespace>();
        /// <summary>The types declared directly in this namespace (nested namespaces may contain further types).</summary>
        public List<CsType> Types = new List<CsType>();

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("namespace ");
            sb.Append(Name.Sanitize());
            sb.Append("\n{\n");

            foreach (var ns in UsingNamespaces)
                sb.Append(ns.ToString());
            if (UsingNamespaces.Any())
                sb.Append('\n');

            foreach (var ns in UsingAliases)
                sb.Append(ns.ToString());
            if (UsingAliases.Any())
                sb.Append('\n');

            bool first = true;
            foreach (var ns in Namespaces)
            {
                if (!first)
                    sb.Append("\n");
                first = false;
                sb.Append(ns.ToString().Indent());
            }
            foreach (var ty in Types)
            {
                if (!first)
                    sb.Append("\n");
                first = false;
                sb.Append(ty.ToString().Indent());
            }

            sb.Append("}\n");
            return sb.ToString();
        }
    }
    #endregion

    #region Members, except types
    /// <summary>
    ///     Base class for C# members, but not types (that’s <see cref="CsType"/> for declarations and <see
    ///     cref="CsTypeName"/> for references).</summary>
    public abstract class CsMember : CsNode
    {
        /// <summary>Custom attributes.</summary>
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        /// <summary>Specifies whether the member is explicitly marked <c>internal</c>.</summary>
        public bool IsInternal;
        /// <summary>Specifies whether the member is explicitly marked <c>private</c>.</summary>
        public bool IsPrivate;
        /// <summary>Specifies whether the member is explicitly marked <c>protected</c>.</summary>
        public bool IsProtected;
        /// <summary>Specifies whether the member is explicitly marked <c>public</c>.</summary>
        public bool IsPublic;
        /// <summary>
        ///     Specifies whether the member is explicitly marked <c>new</c> (a member that hides an inherited member of the
        ///     same name).</summary>
        public bool IsNew;
        /// <summary>
        ///     Specifies whether the member is explicitly marked <c>unsafe</c> (a method or property that contains unsafe
        ///     code).</summary>
        public bool IsUnsafe;

        /// <summary>Returns a <see cref="StringBuilder"/> containing the modifiers.</summary>
        protected virtual StringBuilder modifiersCs()
        {
            var sb = new StringBuilder();
            foreach (var str in CustomAttributes)
            {
                sb.Append(str);
                sb.Append('\n');
            }
            if (IsProtected) sb.Append("protected ");
            if (IsInternal) sb.Append("internal ");
            if (IsPrivate) sb.Append("private ");
            if (IsPublic) sb.Append("public ");
            if (IsNew) sb.Append("new ");
            if (IsUnsafe) sb.Append("unsafe ");
            return sb;
        }
    }

    /// <summary>Base class for C# events and fields.</summary>
    public abstract class CsEventOrField : CsMember
    {
        /// <summary>Specifies whether the member is explicitly marked <c>static</c>.</summary>
        public bool IsStatic;

        /// <summary>The type of the member.</summary>
        public CsTypeName Type;
        /// <summary>The names and initialization expressions for each member declared.</summary>
        public List<CsNameAndExpression> NamesAndInitializers = new List<CsNameAndExpression>();

        /// <summary>Returns a <see cref="StringBuilder"/> containing the modifiers.</summary>
        protected override StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            if (IsStatic) sb.Append("static ");
            return sb;
        }
    }

    /// <summary>Base class for C# methods and properties.</summary>
    public abstract class CsMethodOrProperty : CsMember
    {
        /// <summary>Specifies whether the member is explicitly marked <c>abstract</c>.</summary>
        public bool IsAbstract;
        /// <summary>Specifies whether the member is explicitly marked <c>virtual</c>.</summary>
        public bool IsVirtual;
        /// <summary>Specifies whether the member is explicitly marked <c>override</c>.</summary>
        public bool IsOverride;
        /// <summary>Specifies whether the member is explicitly marked <c>sealed</c>.</summary>
        public bool IsSealed;
        /// <summary>Specifies whether the member is explicitly marked <c>static</c>.</summary>
        public bool IsStatic;
        /// <summary>Specifies whether the member is explicitly marked <c>extern</c>.</summary>
        public bool IsExtern;

        /// <summary>The type of the member (return type in the case of methods).</summary>
        public CsTypeName Type;
        /// <summary>Name of the member.</summary>
        public string Name;
        /// <summary>The interface for which this member is an explicit interface implementation (<c>null</c> if none).</summary>
        public CsTypeName ImplementsFrom;

        /// <summary>Returns a <see cref="StringBuilder"/> containing the modifiers.</summary>
        protected override StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            if (IsAbstract) sb.Append("abstract ");
            if (IsVirtual) sb.Append("virtual ");
            if (IsOverride) sb.Append("override ");
            if (IsSealed) sb.Append("sealed ");
            if (IsStatic) sb.Append("static ");
            if (IsExtern) sb.Append("extern ");
            return sb;
        }
    }

    /// <summary>C# event declaration.</summary>
    public sealed class CsEvent : CsEventOrField
    {
        /// <summary>Specifies whether the event is explicitly marked <c>abstract</c>.</summary>
        public bool IsAbstract;
        /// <summary>Specifies whether the event is explicitly marked <c>virtual</c>.</summary>
        public bool IsVirtual;
        /// <summary>Specifies whether the event is explicitly marked <c>override</c>.</summary>
        public bool IsOverride;
        /// <summary>Specifies whether the event is explicitly marked <c>sealed</c>.</summary>
        public bool IsSealed;

        /// <summary>
        ///     The <c>add</c> and/or <c>remove</c> methods in this event declaration, or <c>null</c> if none are provided.</summary>
        public List<CsSimpleMethod> Methods = null;

        /// <summary>The interface for which this event is an explicit interface implementation (<c>null</c> if none).</summary>
        public CsTypeName ImplementsFrom;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            if (IsAbstract) sb.Append("abstract ");
            if (IsVirtual) sb.Append("virtual ");
            if (IsOverride) sb.Append("override ");
            if (IsSealed) sb.Append("sealed ");
            sb.Append("event ");
            sb.Append(Type.ToString());
            sb.Append(' ');
            if (ImplementsFrom != null)
            {
                sb.Append(ImplementsFrom.ToString());
                sb.Append('.');
            }
            sb.Append(NamesAndInitializers.Select(n => n.ToString()).JoinString(", "));
            if (Methods != null)
            {
                sb.Append("\n{\n");
                sb.Append(Methods.Select(m => m.ToString()).JoinString().Indent());
                sb.Append("}\n");
            }
            else
                sb.Append(";\n");
            return sb.ToString();
        }
    }

    /// <summary>C# field declaration.</summary>
    public sealed class CsField : CsEventOrField
    {
        /// <summary>Specifies whether the field is explicitly marked <c>readonly</c>.</summary>
        public bool IsReadonly;
        /// <summary>Specifies whether the field is explicitly marked <c>const</c>.</summary>
        public bool IsConst;
        /// <summary>Specifies whether the field is explicitly marked <c>volatile</c>.</summary>
        public bool IsVolatile;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            if (IsReadonly) sb.Append("readonly ");
            if (IsConst) sb.Append("const ");
            if (IsVolatile) sb.Append("volatile ");
            sb.Append(Type.ToString());
            sb.Append(' ');
            sb.Append(NamesAndInitializers.Select(n => n.ToString()).JoinString(", "));
            sb.Append(";\n");
            return sb.ToString();
        }
    }

    /// <summary>C# property declaration (indexed properties are <see cref="CsIndexedProperty"/> derived from this).</summary>
    public class CsProperty : CsMethodOrProperty
    {
        /// <summary>The <c>get</c> and/or <c>set</c> methods in this property declaration.</summary>
        public List<CsSimpleMethod> Methods = new List<CsSimpleMethod>();

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(Type.ToString());
            sb.Append(' ');
            if (ImplementsFrom != null)
            {
                sb.Append(ImplementsFrom.ToString());
                sb.Append('.');
            }
            sb.Append(Name.Sanitize());
            sb.Append("\n{\n");
            sb.Append(Methods.Select(m => m.ToString()).JoinString().Indent());
            sb.Append("}\n");
            return sb.ToString();
        }
    }

    /// <summary>C# indexed property declaration.</summary>
    public sealed class CsIndexedProperty : CsProperty
    {
        /// <summary>The parameters of the indexed property.</summary>
        public List<CsParameter> Parameters = new List<CsParameter>();

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(Type.ToString());
            sb.Append(' ');
            if (ImplementsFrom != null)
            {
                sb.Append(ImplementsFrom.ToString());
                sb.Append('.');
            }
            sb.Append("this[");
            sb.Append(Parameters.Select(p => p.ToString()).JoinString(", "));
            sb.Append("]\n{\n");
            sb.Append(Methods.Select(m => m.ToString()).JoinString().Indent());
            sb.Append("}\n");
            return sb.ToString();
        }
    }

    /// <summary>Specifies the type of event or property method.</summary>
    public enum MethodType
    {
        /// <summary>Property <c>get</c> method.</summary>
        Get,
        /// <summary>Property <c>set</c> method.</summary>
        Set,
        /// <summary>Event <c>add</c> method.</summary>
        Add,
        /// <summary>Event <c>remove</c> method.</summary>
        Remove
    }

    /// <summary>C# event or property method.</summary>
    public sealed class CsSimpleMethod : CsMember
    {
        /// <summary>Type of event or property method.</summary>
        public MethodType Type;
        /// <summary>Body of the method.</summary>
        public CsBlock Body;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(Type == MethodType.Get ? "get" : Type == MethodType.Set ? "set" : Type == MethodType.Add ? "add" : Type == MethodType.Remove ? "remove" : null);
            if (Body != null)
            {
                sb.Append('\n');
                sb.Append(Body.ToString());
            }
            else
                sb.Append(";\n");
            return sb.ToString();
        }
    }

    /// <summary>C# method declaration (does not include constructors or operator overloads).</summary>
    public sealed class CsMethod : CsMethodOrProperty
    {
        /// <summary>Specifies whether the method is explicitly marked <c>partial</c>.</summary>
        public bool IsPartial;

        /// <summary>The parameters of the method.</summary>
        public List<CsParameter> Parameters = new List<CsParameter>();
        /// <summary>The generic type parameters if the method is generic; otherwise, <c>null</c>.</summary>
        public List<CsGenericTypeParameter> GenericTypeParameters = null;
        /// <summary>The constraints on each generic type parameter.</summary>
        public Dictionary<string, List<CsGenericTypeConstraint>> GenericTypeConstraints = null;
        /// <summary>
        ///     The body of the method, or <c>null</c> if the method has no body (for example, <c>abstract</c> and
        ///     <c>extern</c> methods).</summary>
        public CsBlock MethodBody;

        private string genericTypeParametersCs()
        {
            if (GenericTypeParameters == null)
                return "";
            return string.Concat("<", GenericTypeParameters.Select(g => g.ToString()).JoinString(", "), ">");
        }

        private string genericTypeConstraintsCs()
        {
            if (GenericTypeConstraints == null)
                return "";
            return GenericTypeConstraints.Select(kvp => " where " + kvp.Key.Sanitize() + " : " + kvp.Value.Select(c => c.ToString()).JoinString(", ")).JoinString();
        }

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            if (IsPartial) sb.Append("partial ");
            sb.Append(Type.ToString());
            sb.Append(' ');
            if (ImplementsFrom != null)
            {
                sb.Append(ImplementsFrom.ToString());
                sb.Append('.');
            }
            sb.Append(Name.Sanitize());
            sb.Append(genericTypeParametersCs());
            sb.Append('(');
            sb.Append(Parameters.Select(p => p.ToString()).JoinString(", "));
            sb.Append(')');
            sb.Append(genericTypeConstraintsCs());

            if (MethodBody != null)
            {
                sb.Append('\n');
                sb.Append(MethodBody.ToString());
            }
            else
                sb.Append(";\n");
            return sb.ToString();
        }

        /// <summary>Overrides <see cref="CsNode.Subnodes"/>.</summary>
        public override IEnumerable<CsNode> Subnodes
        {
            get
            {
                yield return this;

                foreach (var subnode in Type.Subnodes)
                    yield return subnode;

                if (ImplementsFrom != null)
                    foreach (var subnode in ImplementsFrom.Subnodes)
                        yield return subnode;

                foreach (var node in CustomAttributes)
                    foreach (var subnode in node.Subnodes)
                        yield return subnode;

                foreach (var node in Parameters)
                    foreach (var subnode in node.Subnodes)
                        yield return subnode;

                if (GenericTypeParameters != null)
                    foreach (var node in GenericTypeParameters)
                        foreach (var subnode in node.Subnodes)
                            yield return subnode;

                if (GenericTypeConstraints != null)
                    foreach (var values in GenericTypeConstraints.Values)
                        foreach (var node in values)
                            foreach (var subnode in node.Subnodes)
                                yield return subnode;

                if (MethodBody != null)
                    foreach (var subnode in MethodBody.Subnodes)
                        yield return subnode;
            }
        }
    }

    /// <summary>Base class for C# operator overload declarations.</summary>
    public abstract class CsOperatorOverload : CsMember
    {
        /// <summary>
        ///     Specifies whether the operator overload is explicitly marked <c>static</c> (which they all are, so setting
        ///     this to <c>false</c> produces invalid C#).</summary>
        public bool IsStatic;

        /// <summary>Return type of the operator overload.</summary>
        public CsTypeName ReturnType;
        /// <summary>Parameter of the operator overload (first parameter in the case of binary operators).</summary>
        public CsParameter Parameter;
        /// <summary>Operator overload method body.</summary>
        public CsBlock MethodBody;

        /// <summary>Returns a <see cref="StringBuilder"/> containing the modifiers.</summary>
        protected override StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            if (IsStatic) sb.Append("static ");
            return sb;
        }
    }

    /// <summary>Specifies whether a conversion operator overload is implicit or explicit.</summary>
    public enum UserConversionType
    {
        /// <summary>The conversion operator defines an implicit user conversion.</summary>
        Implicit,
        /// <summary>The conversion operator defines an explicit user conversion.</summary>
        Explicit
    }

    /// <summary>
    ///     C# user-defined conversion declaration (also known as cast operator; for example, <c>public static implicit
    ///     operator Vector3(ThreeVector obj) { /* ... */ }</c>).</summary>
    public sealed class CsUserDefinedConversion : CsOperatorOverload
    {
        /// <summary>Whether the user conversion overloaded by this declaration is implicit or explicit.</summary>
        public UserConversionType CastType;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(CastType == UserConversionType.Implicit ? "implicit operator " : "explicit operator ");
            sb.Append(ReturnType.ToString());
            sb.Append('(');
            sb.Append(Parameter.ToString());
            sb.Append(")\n");
            sb.Append(MethodBody.ToString());
            return sb.ToString();
        }
    }

    /// <summary>C# unary operator overload (for example, <c>public static Vector3 operator-(Vector3 obj) { /* ... */ }</c>).</summary>
    public sealed class CsUnaryOperatorOverload : CsOperatorOverload
    {
        /// <summary>The specific unary operator overloaded by this declaration.</summary>
        public UnaryOperator Operator;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(ReturnType.ToString());
            sb.Append(" operator ");
            sb.Append(Operator.ToCs());
            sb.Append('(');
            sb.Append(Parameter.ToString());
            sb.Append(")\n");
            sb.Append(MethodBody.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    ///     C# binary operator overload (for example, <c>public static Vector3 operator+(Vector3 obj, Vector3 obj) { /* ... */
    ///     }</c>).</summary>
    public sealed class CsBinaryOperatorOverload : CsOperatorOverload
    {
        /// <summary>The specific binary operator overloaded by this declaration.</summary>
        public BinaryOperator Operator;
        /// <summary>The second parameter of the binary operator overload.</summary>
        public CsParameter SecondParameter;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(ReturnType.ToString());
            sb.Append(" operator");
            sb.Append(Operator.ToCs());
            sb.Append('(');
            sb.Append(Parameter.ToString());
            sb.Append(", ");
            sb.Append(SecondParameter.ToString());
            sb.Append(")\n");
            sb.Append(MethodBody.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    ///     Specifies whether a constructor explicitly calls a constructor from the base class, a constructor from the same
    ///     class, or neither, before the main block.</summary>
    public enum ConstructorCallType
    {
        /// <summary>The constructor does not call another constructor explicitly.</summary>
        None,
        /// <summary>
        ///     The constructor calls another constructor in the same class (for example, <c>public MyClass() : this(0) { /*
        ///     ... */ }</c>).</summary>
        This,
        /// <summary>
        ///     The constructor calls a constructor from the base class (for example, <c>public MyClass() : base(0) { /* ...
        ///     */ }</c>).</summary>
        Base
    }

    /// <summary>C# constructor declaration.</summary>
    public sealed class CsConstructor : CsMember
    {
        /// <summary>Name of the constructor (must be the same as the name of the containing class).</summary>
        public string Name;
        /// <summary>Constructor parameters.</summary>
        public List<CsParameter> Parameters = new List<CsParameter>();
        /// <summary>Constructor method body.</summary>
        public CsBlock MethodBody;
        /// <summary>
        ///     Specifies whether a constructor explicitly calls a constructor from the base class, a constructor from the
        ///     same class, or neither, before the main block.</summary>
        public ConstructorCallType CallType;
        /// <summary>
        ///     Arguments of the explicit call to another constructor before the main block (see <see cref="CallType"/>), or
        ///     <c>null</c> if none.</summary>
        public List<CsArgument> CallArguments = new List<CsArgument>();
        /// <summary>
        ///     Specifies whether the constructor is explicitly marked <c>static</c> (which it never is, so setting this to
        ///     <c>true</c> produces invalid C#).</summary>
        public bool IsStatic;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            if (IsStatic) sb.Append("static ");
            sb.Append(Name.Sanitize());
            sb.Append('(');
            sb.Append(Parameters.Select(p => p.ToString()).JoinString(", "));
            sb.Append(')');

            if (CallType != ConstructorCallType.None)
            {
                sb.Append(CallType == ConstructorCallType.Base ? " : base(" : CallType == ConstructorCallType.This ? " : this(" : null);
                sb.Append(CallArguments.Select(p => p.ToString()).JoinString(", "));
                sb.Append(')');
            }

            sb.Append('\n');
            sb.Append(MethodBody.ToString());
            return sb.ToString();
        }
    }

    /// <summary>C# destructor declaration (for example, <c>~MyClass() { /* ... */ }</c>).</summary>
    public sealed class CsDestructor : CsMember
    {
        /// <summary>Name of the destructor (must be the same as the name of the containing class).</summary>
        public string Name;
        /// <summary>Destructor method body.</summary>
        public CsBlock MethodBody;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append('~');
            sb.Append(Name.Sanitize());
            sb.Append("()\n");
            sb.Append(MethodBody.ToString());
            return sb.ToString();
        }
    }
    #endregion

    #region Types

    public abstract class CsType : CsMember
    {
        public string Name;
    }
    public abstract class CsTypeCanBeGeneric : CsType
    {
        public List<CsGenericTypeParameter> GenericTypeParameters = null;
        public Dictionary<string, List<CsGenericTypeConstraint>> GenericTypeConstraints = null;

        protected string genericTypeParametersCs()
        {
            if (GenericTypeParameters == null)
                return "";
            return string.Concat("<", GenericTypeParameters.Select(g => g.ToString()).JoinString(", "), ">");
        }

        protected string genericTypeConstraintsCs()
        {
            if (GenericTypeConstraints == null)
                return "";
            return GenericTypeConstraints.Select(kvp => " where " + kvp.Key.Sanitize() + " : " + kvp.Value.Select(c => c.ToString()).JoinString(", ")).JoinString();
        }

        protected IEnumerable<CsNode> SubnodesGenericTypeParameters()
        {
            if (GenericTypeParameters != null)
                return GenericTypeParameters.SelectMany(gtp => gtp.Subnodes);
            return Enumerable.Empty<CsNode>();
        }

        protected IEnumerable<CsNode> SubnodesGenericTypeConstraints()
        {
            if (GenericTypeConstraints != null)
                return GenericTypeConstraints.Values.SelectMany(gtcs => gtcs).SelectMany(gtc => gtc.Subnodes);
            return Enumerable.Empty<CsNode>();
        }
    }
    public abstract class CsTypeLevel2 : CsTypeCanBeGeneric
    {
        public bool IsPartial;

        public List<CsTypeName> BaseTypes = null;
        public List<CsMember> Members = new List<CsMember>();

        protected abstract string typeTypeCs { get; }
        protected override sealed StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            moreModifiers(sb);
            if (IsPartial) sb.Append("partial ");
            return sb;
        }
        public virtual void moreModifiers(StringBuilder sb) { }
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(typeTypeCs);
            sb.Append(' ');
            sb.Append(Name.Sanitize());
            sb.Append(genericTypeParametersCs());
            if (BaseTypes != null && BaseTypes.Any())
            {
                sb.Append(" : ");
                sb.Append(BaseTypes.Select(ty => ty.ToString()).JoinString(", "));
            }
            sb.Append(genericTypeConstraintsCs());
            if (Members.Count == 0)
                sb.Append(" { }\n");
            else
            {
                sb.Append("\n{\n");
                CsMember prevMember = null;
                foreach (var member in Members)
                {
                    if (prevMember != null && (!(member is CsField) || !(prevMember is CsField)))
                        sb.Append('\n');
                    sb.Append(member.ToString().Indent());
                    prevMember = member;
                }
                sb.Append("}\n");
            }
            return sb.ToString();
        }

        public override IEnumerable<CsNode> Subnodes
        {
            get
            {
                foreach (var subnode in SubnodesGenericTypeParameters())
                    yield return subnode;

                if (BaseTypes != null)
                    foreach (var node in BaseTypes)
                        foreach (var subnode in node.Subnodes)
                            yield return subnode;

                foreach (var subnode in SubnodesGenericTypeConstraints())
                    yield return subnode;

                foreach (var node in Members)
                    foreach (var subnode in node.Subnodes)
                        yield return subnode;
            }
        }
    }
    public sealed class CsInterface : CsTypeLevel2
    {
        protected override string typeTypeCs { get { return "interface"; } }
    }
    public sealed class CsStruct : CsTypeLevel2
    {
        protected override string typeTypeCs { get { return "struct"; } }
    }
    public sealed class CsClass : CsTypeLevel2
    {
        public bool IsAbstract, IsSealed, IsStatic;

        protected override string typeTypeCs { get { return "class"; } }
        public override void moreModifiers(StringBuilder sb)
        {
            if (IsAbstract) sb.Append("abstract ");
            if (IsSealed) sb.Append("sealed ");
            if (IsStatic) sb.Append("static ");
        }
    }
    public sealed class CsDelegate : CsTypeCanBeGeneric
    {
        public CsTypeName ReturnType;
        public List<CsParameter> Parameters = new List<CsParameter>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append("delegate ");
            sb.Append(ReturnType.ToString());
            sb.Append(' ');
            sb.Append(Name.Sanitize());
            sb.Append(genericTypeParametersCs());
            sb.Append('(');
            sb.Append(Parameters.Select(p => p.ToString()).JoinString(", "));
            sb.Append(')');
            sb.Append(genericTypeConstraintsCs());
            sb.Append(";\n");
            return sb.ToString();
        }

        public override IEnumerable<CsNode> Subnodes
        {
            get
            {
                foreach (var subnode in ReturnType.Subnodes)
                    yield return subnode;

                foreach (var subnode in SubnodesGenericTypeParameters())
                    yield return subnode;

                foreach (var node in Parameters)
                    foreach (var subnode in node.Subnodes)
                        yield return subnode;

                foreach (var subnode in SubnodesGenericTypeConstraints())
                    yield return subnode;
            }
        }
    }
    public sealed class CsEnum : CsType
    {
        public CsTypeName BaseType;
        public List<CsEnumValue> EnumValues = new List<CsEnumValue>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append("enum ");
            sb.Append(Name.Sanitize());
            if (!EnumValues.Any())
            {
                sb.Append(" { }\n");
                return sb.ToString();
            }
            sb.Append("\n{\n");
            sb.Append(EnumValues.Select(ev => ev.ToString()).JoinString(",\n").Indent());
            sb.Append("\n}\n");
            return sb.ToString();
        }
    }
    public sealed class CsEnumValue : CsNode
    {
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        public string Name;
        public CsExpression LiteralValue;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder(CustomAttributes.Select(c => c.ToString() + '\n').JoinString());
            sb.Append(Name.Sanitize());
            if (LiteralValue != null)
            {
                sb.Append(" = ");
                sb.Append(LiteralValue.ToString());
            }
            return sb.ToString();
        }
    }
    #endregion

    #region Parameters, simple names and type names
    public sealed class CsParameter : CsNode
    {
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        public CsTypeName Type;
        public string Name;
        public CsExpression DefaultValue;
        public bool IsThis, IsOut, IsRef, IsParams;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            // If 'Type' is null, this is a parameter to a lambda expression where the type is not specified.
            if (Type == null)
                return Name.Sanitize();

            var sb = new StringBuilder(CustomAttributes.Select(c => c.ToString() + ' ').JoinString());
            if (IsThis) sb.Append("this ");
            if (IsParams) sb.Append("params ");
            if (IsOut) sb.Append("out ");
            if (IsRef) sb.Append("ref ");
            sb.Append(Type.ToString());
            sb.Append(' ');
            sb.Append(Name.Sanitize());
            if (DefaultValue != null)
            {
                sb.Append(" = ");
                sb.Append(DefaultValue.ToString());
            }
            return sb.ToString();
        }
    }

    public abstract class CsIdentifier : CsNode
    {
        public abstract bool EndsWithGenerics { get; }
    }
    public sealed class CsNameIdentifier : CsIdentifier
    {
        public string Name;
        public List<CsTypeName> GenericTypeArguments = null;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return GenericTypeArguments == null ? Name.Sanitize() : string.Concat(Name.Sanitize(), '<', GenericTypeArguments.Select(p => p.ToString()).JoinString(", "), '>'); }
        public override bool EndsWithGenerics { get { return GenericTypeArguments != null && GenericTypeArguments.Count > 0; } }
    }
    public sealed class CsKeywordIdentifier : CsIdentifier
    {
        public string Keyword;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Keyword; }
        public override bool EndsWithGenerics { get { return false; } }
    }

    public abstract class CsTypeName : CsNode
    {
        public virtual string GetSingleIdentifier() { return null; }
    }
    public sealed class CsEmptyGenericParameter : CsTypeName
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return ""; }
    }
    public sealed class CsConcreteTypeName : CsTypeName
    {
        public bool HasGlobal;
        public List<CsIdentifier> Parts = new List<CsIdentifier>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return (HasGlobal ? "global::" : "") + Parts.Select(p => p.ToString()).JoinString("."); }
        public override string GetSingleIdentifier()
        {
            if (HasGlobal || Parts.Count != 1)
                return null;
            var identifier = Parts[0] as CsNameIdentifier;
            return identifier != null && identifier.GenericTypeArguments == null ? identifier.Name : null;
        }
    }
    public sealed class CsArrayTypeName : CsTypeName
    {
        public CsTypeName InnerType;
        public List<int> ArrayRanks = new List<int> { 1 };
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return InnerType.ToString() + ArrayRanks.Select(rank => string.Concat("[", new string(',', rank - 1), "]")).JoinString(); }
    }
    public sealed class CsPointerTypeName : CsTypeName
    {
        public CsTypeName InnerType;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return InnerType.ToString() + "*"; }
    }
    public sealed class CsNullableTypeName : CsTypeName
    {
        public CsTypeName InnerType;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return InnerType.ToString() + "?"; }
    }
    #endregion

    #region Generics
    public enum VarianceMode { Invariant, Covariant, Contravariant }
    public sealed class CsGenericTypeParameter : CsNode
    {
        public VarianceMode Variance;
        public string Name;
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return CustomAttributes.Select(c => c.ToString() + ' ').JoinString() + (Variance == VarianceMode.Covariant ? "out " : Variance == VarianceMode.Contravariant ? "in " : "") + Name.Sanitize();
        }
    }

    public abstract class CsGenericTypeConstraint : CsNode { }
    public sealed class CsGenericTypeConstraintNew : CsGenericTypeConstraint { public override string ToString() { return "new()"; } }
    public sealed class CsGenericTypeConstraintClass : CsGenericTypeConstraint { public override string ToString() { return "class"; } }
    public sealed class CsGenericTypeConstraintStruct : CsGenericTypeConstraint { public override string ToString() { return "struct"; } }
    public sealed class CsGenericTypeConstraintBaseClass : CsGenericTypeConstraint
    {
        public CsTypeName BaseClass;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return BaseClass.ToString(); }
    }
    #endregion

    #region Custom attributes
    public sealed class CsCustomAttribute : CsNode
    {
        public CsTypeName Type;
        public List<CsArgument> Arguments = new List<CsArgument>();
        public List<CsNameAndExpression> PropertySetters = new List<CsNameAndExpression>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Arguments.Count + PropertySetters.Count == 0)
                return Type.ToString();
            return string.Concat(Type.ToString(), '(', Arguments.Concat<CsNode>(PropertySetters).Select(p => p.ToString()).JoinString(", "), ')');
        }
    }
    public enum CustomAttributeLocation { None, Assembly, Module, Type, Method, Property, Field, Event, Param, Return, Typevar }
    public sealed class CsCustomAttributeGroup : CsNode
    {
        public CustomAttributeLocation Location;
        public List<CsCustomAttribute> CustomAttributes;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder("[");
            switch (Location)
            {
                case CustomAttributeLocation.Assembly: sb.Append("assembly: "); break;
                case CustomAttributeLocation.Module: sb.Append("module: "); break;
                case CustomAttributeLocation.Type: sb.Append("type: "); break;
                case CustomAttributeLocation.Method: sb.Append("method: "); break;
                case CustomAttributeLocation.Property: sb.Append("property: "); break;
                case CustomAttributeLocation.Field: sb.Append("field: "); break;
                case CustomAttributeLocation.Event: sb.Append("event: "); break;
                case CustomAttributeLocation.Param: sb.Append("param: "); break;
                case CustomAttributeLocation.Return: sb.Append("return: "); break;
                case CustomAttributeLocation.Typevar: sb.Append("typevar: "); break;
            }
            sb.Append(CustomAttributes.Select(c => c.ToString()).JoinString(", "));
            sb.Append(']');
            return sb.ToString();
        }
    }
    #endregion

    #region Statements
    public abstract class CsStatement : CsNode
    {
        public List<string> GotoLabels;
        protected string gotoLabels() { return GotoLabels == null ? "" : GotoLabels.Select(g => g.Sanitize() + ':').JoinString(" ") + (this is CsEmptyStatement ? " " : "\n"); }
    }
    public sealed class CsEmptyStatement : CsStatement { public override string ToString() { return gotoLabels() + ";\n"; } }
    public sealed class CsBlock : CsStatement
    {
        public List<CsStatement> Statements = new List<CsStatement>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("{\n");
            foreach (var st in Statements)
                sb.Append(st.ToString().Indent());
            sb.Append("}\n");
            return sb.ToString();
        }
    }
    public abstract class CsOptionalExpressionStatement : CsStatement
    {
        public CsExpression Expression;
        public abstract string Keyword { get; }
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Expression == null)
                return string.Concat(gotoLabels(), Keyword, ";\n");
            return string.Concat(gotoLabels(), Keyword, ' ', Expression.ToString(), ";\n");
        }
    }
    public sealed class CsReturnStatement : CsOptionalExpressionStatement { public override string Keyword { get { return "return"; } } }
    public sealed class CsThrowStatement : CsOptionalExpressionStatement { public override string Keyword { get { return "throw"; } } }
    public abstract class CsBlockStatement : CsStatement { public CsBlock Block; }
    public sealed class CsCheckedStatement : CsBlockStatement { public override string ToString() { return string.Concat(gotoLabels(), "checked\n", Block.ToString()); } }
    public sealed class CsUncheckedStatement : CsBlockStatement { public override string ToString() { return string.Concat(gotoLabels(), "unchecked\n", Block.ToString()); } }
    public sealed class CsUnsafeStatement : CsBlockStatement { public override string ToString() { return string.Concat(gotoLabels(), "unsafe\n", Block.ToString()); } }
    public sealed class CsSwitchStatement : CsStatement
    {
        public CsExpression SwitchOn;
        public List<CsSwitchCase> Cases = new List<CsSwitchCase>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "switch (", SwitchOn.ToString(), ")\n{\n", Cases.Select(c => c.ToString().Indent()).JoinString("\n"), "}\n"); }
    }
    public sealed class CsSwitchCase : CsNode
    {
        public List<CsExpression> CaseValues = new List<CsExpression>();  // use a 'null' expression for the 'default' label
        public List<CsStatement> Statements;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(CaseValues.Select(c => c == null ? "default:\n" : "case " + c.ToString() + ":\n").JoinString(), Statements.Select(s => s.ToString().Indent()).JoinString()); }
    }
    public sealed class CsExpressionStatement : CsStatement
    {
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), Expression.ToString(), ";\n"); }
    }
    public sealed class CsVariableDeclarationStatement : CsStatement
    {
        public CsTypeName Type;
        public List<CsNameAndExpression> NamesAndInitializers = new List<CsNameAndExpression>();
        public bool IsConst;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            if (IsConst) sb.Append("const ");
            sb.Append(Type.ToString());
            sb.Append(' ');
            sb.Append(NamesAndInitializers.Select(n => n.ToString()).JoinString(", "));
            sb.Append(";\n");
            return sb.ToString();
        }
    }
    public sealed class CsForeachStatement : CsStatement
    {
        public CsTypeName VariableType;
        public string VariableName;
        public CsExpression LoopExpression;
        public CsStatement Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "foreach (", VariableType == null ? "" : VariableType.ToString() + ' ', VariableName.Sanitize(), " in ", LoopExpression.ToString(), ")\n", Body is CsBlock ? Body.ToString() : Body.ToString().Indent()); }
    }
    public sealed class CsForStatement : CsStatement
    {
        public List<CsStatement> InitializationStatements = new List<CsStatement>();
        public CsExpression TerminationCondition;
        public List<CsExpression> LoopExpressions = new List<CsExpression>();
        public CsStatement Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("for (");
            sb.Append(InitializationStatements.Select(i => i.ToString().Trim().TrimEnd(';')).JoinString(", "));
            sb.Append("; ");
            if (TerminationCondition != null)
                sb.Append(TerminationCondition.ToString());
            sb.Append("; ");
            sb.Append(LoopExpressions.Select(l => l.ToString()).JoinString(", "));
            sb.Append(")\n");
            sb.Append(Body is CsBlock ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }
    public sealed class CsUsingStatement : CsStatement
    {
        public CsStatement InitializationStatement;
        public CsStatement Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("using (");
            sb.Append(InitializationStatement.ToString().Trim().TrimEnd(';'));
            sb.Append(")\n");
            sb.Append(Body is CsBlock || Body is CsUsingStatement ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }
    public sealed class CsFixedStatement : CsStatement
    {
        public CsStatement InitializationStatement;
        public CsStatement Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("fixed (");
            sb.Append(InitializationStatement.ToString().Trim().TrimEnd(';'));
            sb.Append(")\n");
            sb.Append(Body is CsBlock || Body is CsFixedStatement ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }
    public sealed class CsIfStatement : CsStatement
    {
        public CsExpression IfExpression;
        public CsStatement Statement;
        public CsStatement ElseStatement;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (ElseStatement == null)
                return string.Concat(gotoLabels(), "if (", IfExpression.ToString(), ")\n", Statement is CsBlock ? Statement.ToString() : Statement.ToString().Indent());
            else if (ElseStatement is CsIfStatement)
                return string.Concat(gotoLabels(), "if (", IfExpression.ToString(), ")\n", Statement is CsBlock ? Statement.ToString() : Statement.ToString().Indent(), "else ", ElseStatement.ToString());
            else
                return string.Concat(gotoLabels(), "if (", IfExpression.ToString(), ")\n", Statement is CsBlock ? Statement.ToString() : Statement.ToString().Indent(), "else\n", ElseStatement is CsBlock ? ElseStatement.ToString() : ElseStatement.ToString().Indent());
        }
    }
    public abstract class CsExpressionBlockStatement : CsStatement
    {
        public CsExpression Expression;
        public CsStatement Body;
        public abstract string Keyword { get; }
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), Keyword, " (", Expression.ToString(), ")\n", Body is CsBlock ? Body.ToString() : Body.ToString().Indent()); }
    }
    public sealed class CsWhileStatement : CsExpressionBlockStatement { public override string Keyword { get { return "while"; } } }
    public sealed class CsLockStatement : CsExpressionBlockStatement { public override string Keyword { get { return "lock"; } } }
    public sealed class CsDoWhileStatement : CsStatement
    {
        public CsExpression WhileExpression;
        public CsStatement Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "do\n", Body is CsBlock ? Body.ToString() : Body.ToString().Indent(), "while (", WhileExpression.ToString(), ");\n"); }
    }
    public sealed class CsTryStatement : CsStatement
    {
        public CsBlock TryBody;
        public List<CsCatchClause> Catches = new List<CsCatchClause>();
        public CsBlock Finally;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("try\n");
            sb.Append(TryBody.ToString());
            foreach (var ctch in Catches)
                sb.Append(ctch.ToString());
            if (Finally != null)
            {
                sb.Append("finally\n");
                sb.Append(Finally.ToString());
            }
            return sb.ToString();
        }
    }
    public sealed class CsCatchClause : CsNode
    {
        public CsTypeName Type;
        public string Name;
        public CsBlock Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder("catch");
            if (Type != null)
            {
                sb.Append(" (");
                sb.Append(Type.ToString());
                if (Name != null)
                {
                    sb.Append(' ');
                    sb.Append(Name.Sanitize());
                }
                sb.Append(")");
            }
            sb.Append("\n");
            sb.Append(Body is CsBlock ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }
    public sealed class CsGotoStatement : CsStatement { public string Label; public override string ToString() { return string.Concat(gotoLabels(), "goto ", Label.Sanitize(), ";\n"); } }
    public sealed class CsContinueStatement : CsStatement { public override string ToString() { return string.Concat(gotoLabels(), "continue;\n"); } }
    public sealed class CsBreakStatement : CsStatement { public override string ToString() { return string.Concat(gotoLabels(), "break;\n"); } }
    public sealed class CsGotoDefaultStatement : CsStatement { public override string ToString() { return string.Concat(gotoLabels(), "goto default;\n"); } }
    public sealed class CsGotoCaseStatement : CsStatement { public CsExpression Expression; public override string ToString() { return string.Concat(gotoLabels(), "goto case ", Expression.ToString(), ";\n"); } }
    public sealed class CsYieldBreakStatement : CsStatement { public override string ToString() { return string.Concat(gotoLabels(), "yield break;\n"); } }
    public sealed class CsYieldReturnStatement : CsStatement { public CsExpression Expression; public override string ToString() { return string.Concat(gotoLabels(), "yield return ", Expression.ToString(), ";\n"); } }
    #endregion

    #region Expressions
    /// <summary>Base class for all C# expressions.</summary>
    public abstract class CsExpression : CsNode
    {
        internal virtual ResolveContext ToResolveContext(NameResolver resolver, bool isChecked) { return new ResolveContextExpression(ToLinqExpression(resolver, isChecked)); }
        internal static ResolveContext ToResolveContext(CsExpression expr, NameResolver resolver, bool isChecked) { return expr.ToResolveContext(resolver, isChecked); }
        /// <summary>
        ///     Converts this expression parse tree into an expression using the <c>System.Linq.Expressions</c> namespace. The
        ///     expression thus returned can be used in scenarios surrounding <see cref="IQueryable"/>, such as LINQ-to-SQL,
        ///     EntityFramework and many others.</summary>
        /// <param name="resolver">
        ///     Defines the rules of how to resolve names. Use this both to define which assemblies the code should be allowed
        ///     to access as well as to declare variables and constants.</param>
        /// <param name="isChecked">
        ///     Specifies whether arithmetic operations (such as addition) should assume checked arithmetic or unchecked. The
        ///     <c>checked()</c> and <c>unchecked()</c> expressions override this, so this only applies to expressions outside
        ///     of those.</param>
        /// <returns>
        ///     An expression deriving from <see cref="System.Linq.Expressions.Expression"/>.</returns>
        public abstract Expression ToLinqExpression(NameResolver resolver, bool isChecked);
    }

    /// <summary>Specifies the assignment operator used in a <see cref="CsAssignmentExpression"/>.</summary>
    public enum AssignmentOperator
    {
        /// <summary>The <c>=</c> operator.</summary>
        Eq,
        /// <summary>The <c>*=</c> operator.</summary>
        TimesEq,
        /// <summary>The <c>/=</c> operator.</summary>
        DivEq,
        /// <summary>The <c>%=</c> operator.</summary>
        ModEq,
        /// <summary>The <c>+=</c> operator.</summary>
        PlusEq,
        /// <summary>The <c>-=</c> operator.</summary>
        MinusEq,
        /// <summary>The <c>&lt;&lt;=</c> operator.</summary>
        ShlEq,
        /// <summary>The <c>&gt;&gt;=</c> operator.</summary>
        ShrEq,
        /// <summary>The <c>&amp;=</c> operator.</summary>
        AndEq,
        /// <summary>The <c>^=</c> operator.</summary>
        XorEq,
        /// <summary>The <c>|=</c> operator.</summary>
        OrEq
    }

    /// <summary>C# assignment expression (for example, <c>a += b</c>).</summary>
    public sealed class CsAssignmentExpression : CsExpression
    {
        /// <summary>The assignment operator used.</summary>
        public AssignmentOperator Operator;
        /// <summary>The expression on the left of the operator.</summary>
        public CsExpression Left;
        /// <summary>The expression on the right of the operator.</summary>
        public CsExpression Right;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return string.Concat(
                Left.ToString(),
                Operator == AssignmentOperator.Eq ? " = " :
                Operator == AssignmentOperator.TimesEq ? " *= " :
                Operator == AssignmentOperator.DivEq ? " /= " :
                Operator == AssignmentOperator.ModEq ? " %= " :
                Operator == AssignmentOperator.PlusEq ? " += " :
                Operator == AssignmentOperator.MinusEq ? " -= " :
                Operator == AssignmentOperator.ShlEq ? " <<= " :
                Operator == AssignmentOperator.ShrEq ? " >>= " :
                Operator == AssignmentOperator.AndEq ? " &= " :
                Operator == AssignmentOperator.XorEq ? " ^= " :
                Operator == AssignmentOperator.OrEq ? " |= " : null,
                Right.ToString());
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsConditionalExpression : CsExpression
    {
        public CsExpression Condition, TruePart, FalsePart;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return string.Concat(Condition.ToString(), " ? ", TruePart.ToString(), " : ", FalsePart.ToString());
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public enum BinaryOperator
    {
        // The following operators are used both in CsBinaryOperatorExpression and in CsBinaryOperatorOverload
        Times, Div, Mod, Plus, Minus, Shl, Shr, Less, Greater, LessEq, GreaterEq, Eq, NotEq, And, Xor, Or,

        // The following operators are used only in CsBinaryOperatorExpression
        AndAnd, OrOr, Coalesce
    }
    public sealed class CsBinaryOperatorExpression : CsExpression
    {
        public BinaryOperator Operator;
        public CsExpression Left, Right;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(Left.ToString(), ' ', Operator.ToCs(), ' ', Right.ToString()); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked)
        {
            var left = Left.ToLinqExpression(resolver, isChecked);
            var right = Right.ToLinqExpression(resolver, isChecked);
            if (left.Type != right.Type)
            {
                var conv = Conversion.Implicit(left.Type, right.Type);
                if (conv != null)
                    left = Expression.Convert(left, right.Type);
                else if ((conv = Conversion.Implicit(right.Type, left.Type)) != null)
                    right = Expression.Convert(right, left.Type);
            }

            switch (Operator)
            {
                case BinaryOperator.Times:
                    return isChecked ? Expression.MultiplyChecked(left, right) : Expression.Multiply(left, right);
                case BinaryOperator.Div:
                    return Expression.Divide(left, right);
                case BinaryOperator.Mod:
                    return Expression.Modulo(left, right);
                case BinaryOperator.Plus:
                    return isChecked ? Expression.AddChecked(left, right) : Expression.Add(left, right);
                case BinaryOperator.Minus:
                    return isChecked ? Expression.SubtractChecked(left, right) : Expression.Subtract(left, right);
                case BinaryOperator.Shl:
                    return Expression.LeftShift(left, right);
                case BinaryOperator.Shr:
                    return Expression.RightShift(left, right);
                case BinaryOperator.Less:
                    return Expression.LessThan(left, right);
                case BinaryOperator.Greater:
                    return Expression.GreaterThan(left, right);
                case BinaryOperator.LessEq:
                    return Expression.LessThanOrEqual(left, right);
                case BinaryOperator.GreaterEq:
                    return Expression.GreaterThanOrEqual(left, right);
                case BinaryOperator.Eq:
                    return Expression.Equal(left, right);
                case BinaryOperator.NotEq:
                    return Expression.NotEqual(left, right);
                case BinaryOperator.And:
                    return Expression.And(left, right);
                case BinaryOperator.Xor:
                    return Expression.ExclusiveOr(left, right);
                case BinaryOperator.Or:
                    return Expression.Or(left, right);
                case BinaryOperator.AndAnd:
                    return Expression.AndAlso(left, right);
                case BinaryOperator.OrOr:
                    return Expression.OrElse(left, right);
                case BinaryOperator.Coalesce:
                    return Expression.Coalesce(left, right);
                default:
                    throw new InvalidOperationException("Unexpected binary operator: " + Operator);
            }
        }
    }
    public enum BinaryTypeOperator { Is, As }
    public sealed class CsBinaryTypeOperatorExpression : CsExpression
    {
        public BinaryTypeOperator Operator;
        public CsExpression Left;
        public CsTypeName Right;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return string.Concat(
                Left.ToString(),
                Operator == BinaryTypeOperator.Is ? " is " :
                Operator == BinaryTypeOperator.As ? " as " : null,
                Right.ToString()
            );
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public enum UnaryOperator
    {
        // The following unary operators are used both in CsUnaryOperatorExpression and in CsUnaryOperatorOverload
        Plus, Minus, Not, Neg, PrefixInc, PrefixDec,

        // The following unary operators are used only in CsUnaryOperatorExpression
        PostfixInc, PostfixDec, PointerDeref, AddressOf,

        // The following unary operators are used only in CsUnaryOperatorOverload
        True, False
    }
    public sealed class CsUnaryOperatorExpression : CsExpression
    {
        public UnaryOperator Operator;
        public CsExpression Operand;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Operator == UnaryOperator.PostfixInc)
                return Operand.ToString() + "++";
            if (Operator == UnaryOperator.PostfixDec)
                return Operand.ToString() + "--";

            // Special case: We could have multiple + or - unary operators following one another; in those cases
            // we need to add an extra space so that it doesn’t turn into the prefix ++ or -- operator
            if (Operator == UnaryOperator.Plus && Operand is CsUnaryOperatorExpression && ((CsUnaryOperatorExpression) Operand).Operator == UnaryOperator.Plus)
                return "+ " + Operand.ToString();
            else if (Operator == UnaryOperator.Minus && Operand is CsUnaryOperatorExpression && ((CsUnaryOperatorExpression) Operand).Operator == UnaryOperator.Minus)
                return "- " + Operand.ToString();
            else
                return Operator.ToCs() + Operand.ToString();
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked)
        {
            // Special case: if this is a unary minus operator that operates on a CsNumberLiteralExpression,
            // simply add the "-" and parse the number directly
            if (Operator == UnaryOperator.Minus && Operand is CsNumberLiteralExpression)
                return Expression.Constant(ParserUtil.ParseNumericLiteral("-" + ((CsNumberLiteralExpression) Operand).Literal));
            throw new NotImplementedException();
        }
    }
    public sealed class CsCastExpression : CsExpression
    {
        public CsTypeName Type;
        public CsExpression Operand;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat('(', Type.ToString(), ") ", Operand.ToString()); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked)
        {
            var operand = ToResolveContext(Operand, resolver, isChecked);
            var type = resolver.ResolveType(Type);
            if (operand is ResolveContextLambda)
                throw new NotImplementedException();
            return Expression.Convert(operand.ToExpression(), type);
        }
    }
    public enum MemberAccessType { Regular, PointerDeref };
    public sealed class CsMemberAccessExpression : CsExpression
    {
        public MemberAccessType AccessType;
        public CsExpression Left;
        public CsIdentifier Right;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(Left.ToString(), AccessType == MemberAccessType.PointerDeref ? "->" : ".", Right.ToString()); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return ToResolveContext(resolver, isChecked).ToExpression(); }
        internal override ResolveContext ToResolveContext(NameResolver resolver, bool isChecked)
        {
            if (AccessType == MemberAccessType.PointerDeref)
                throw new InvalidOperationException("Pointer dereference is not supported in LINQ expressions.");
            return resolver.ResolveSimpleName(Right, ToResolveContext(Left, resolver, isChecked));
        }
    }
    public sealed class CsFunctionCallExpression : CsExpression
    {
        public bool IsIndexer;
        public CsExpression Left;
        public List<CsArgument> Arguments = new List<CsArgument>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(Left.ToString(), IsIndexer ? '[' : '(', Arguments.Select(p => p.ToString()).JoinString(", "), IsIndexer ? ']' : ')'); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return ToResolveContext(resolver, isChecked).ToExpression(); }
        internal override ResolveContext ToResolveContext(NameResolver resolver, bool isChecked)
        {
            if (Arguments.Any(a => a.Mode != ArgumentMode.In))
                throw new NotImplementedException("out and ref parameters are not implemented.");

            var left = ToResolveContext(Left, resolver, isChecked);
            var resolvedArguments = Arguments.Select(a => new ArgumentInfo(a.Name, ToResolveContext(a.Expression, resolver, isChecked), a.Mode));

            if (IsIndexer)
            {
                var property = ParserUtil.ResolveOverloads(
                    left.ExpressionType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Select(p => Tuple.Create(p, p.GetIndexParameters()))
                        .ToList(),
                    resolvedArguments,
                    resolver);
                throw new NotImplementedException();
            }

            var leftMg = left as ResolveContextMethodGroup;
            if (leftMg != null)
            {
                // Try non-extension methods first, then extension methods
                for (int i = 0; i < 2; i++)
                {
                    var method = ParserUtil.ResolveOverloads(
                        leftMg.MethodGroup.Where(mg => mg.IsExtensionMethod == (i == 1)).Select(mg => Tuple.Create(mg.Method, mg.Method.GetParameters())).ToList(),
                        // For extension methods, add the expression that pretends to be the “this” instance as the first argument
                        i == 0 ? resolvedArguments : new[] { new ArgumentInfo(null, leftMg.Parent, ArgumentMode.In) }.Concat(resolvedArguments),
                        resolver);
                    if (method != null)
                        return new ResolveContextExpression(Expression.Call(method.Member.IsStatic ? null : leftMg.Parent.ToExpression(), method.Member, method.Parameters.Select(arg =>
                        {
                            var argExpr = arg.Argument.ToExpression();
                            return (argExpr.Type != arg.ParameterType) ? Expression.Convert(argExpr, arg.ParameterType) : argExpr;
                        })), wasAnonymousFunction: false);
                }
                throw new InvalidOperationException("Cannot determine which method overload to use for “{0}”. Either type inference failed or the call is ambiguous.".Fmt(leftMg.MethodName));
            }

            throw new NotImplementedException();
        }
    }
    public abstract class CsTypeOperatorExpression : CsExpression { public CsTypeName Type; }
    public sealed class CsTypeofExpression : CsTypeOperatorExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("typeof(", Type.ToString(), ')'); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsSizeofExpression : CsTypeOperatorExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("sizeof(", Type.ToString(), ')'); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsDefaultExpression : CsTypeOperatorExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("default(", Type.ToString(), ')'); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public abstract class CsCheckedUncheckedExpression : CsExpression { public CsExpression Subexpression; }
    public sealed class CsCheckedExpression : CsCheckedUncheckedExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("checked(", Subexpression.ToString(), ')'); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsUncheckedExpression : CsCheckedUncheckedExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("unchecked(", Subexpression.ToString(), ')'); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsIdentifierExpression : CsExpression
    {
        public CsIdentifier Identifier;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Identifier.ToString(); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return ToResolveContext(resolver, isChecked).ToExpression(); }
        internal override ResolveContext ToResolveContext(NameResolver resolver, bool isChecked) { return resolver.ResolveSimpleName(Identifier); }
    }
    public sealed class CsParenthesizedExpression : CsExpression
    {
        public CsExpression Subexpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat('(', Subexpression.ToString(), ')'); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return Subexpression.ToLinqExpression(resolver, isChecked); }
    }
    public sealed class CsStringLiteralExpression : CsExpression
    {
        public string Literal;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            bool useVerbatim;

            // If the string contains any of the escapable characters, use those escape sequences.
            if (Literal.Any(ch => "\0\a\b\f\n\r\t\v".Contains(ch)))
                useVerbatim = false;
            // Otherwise, if the string contains a double-quote or backslash, use verbatim.
            else if (Literal.Any(ch => ch == '"' || ch == '\\'))
                useVerbatim = true;
            // In all other cases, use escape sequences.
            else
                useVerbatim = false;

            if (useVerbatim)
                return string.Concat('@', '"', Literal.Split('"').JoinString("\"\""), '"');
            else
                return string.Concat('"', Literal.Select(ch => ch.CsEscape(false, true)).JoinString(), '"');
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return Expression.Constant(Literal); }
    }
    public sealed class CsCharacterLiteralExpression : CsExpression
    {
        public char Literal;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat('\'', Literal.CsEscape(true, false), '\''); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsNumberLiteralExpression : CsExpression
    {
        public string Literal;  // Could break this down further, but this is the safest
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Literal; }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return Expression.Constant(ParserUtil.ParseNumericLiteral(Literal)); }
    }
    public sealed class CsBooleanLiteralExpression : CsExpression
    {
        public bool Literal;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Literal ? "true" : "false"; }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsNullExpression : CsExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "null"; }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsThisExpression : CsExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "this"; }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsBaseExpression : CsExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "base"; }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsNewConstructorExpression : CsExpression
    {
        public CsTypeName Type;
        public List<CsArgument> Arguments = new List<CsArgument>();
        public List<CsExpression> Initializers;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder("new ");
            sb.Append(Type.ToString());
            if (Arguments.Any() || Initializers == null)
            {
                sb.Append('(');
                sb.Append(Arguments.Select(p => p.ToString()).JoinString(", "));
                sb.Append(')');
            }
            if (Initializers != null)
            {
                sb.Append(" { ");
                sb.Append(Initializers.Select(ini => ini.ToString()).JoinString(", "));
                sb.Append(" }");
            }
            return sb.ToString();
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsNewAnonymousTypeExpression : CsExpression
    {
        public List<CsExpression> Initializers = new List<CsExpression>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("new { ", Initializers.Select(ini => ini.ToString()).JoinString(", "), " }"); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsNewImplicitlyTypedArrayExpression : CsExpression
    {
        public List<CsExpression> Items = new List<CsExpression>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Items.Count == 0 ? "new[] { }" : string.Concat("new[] { ", Items.Select(p => p.ToString()).JoinString(", "), " }"); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsNewArrayExpression : CsExpression
    {
        public CsTypeName Type;
        public List<CsExpression> SizeExpressions = new List<CsExpression>();
        public List<int> AdditionalRanks = new List<int>();
        public List<CsExpression> Items;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder("new ");
            sb.Append(Type.ToString());
            sb.Append('[');
            sb.Append(SizeExpressions.Select(s => s.ToString()).JoinString(", "));
            sb.Append(']');
            sb.Append(AdditionalRanks.Select(a => "[" + new string(',', a - 1) + ']').JoinString());
            if (Items != null)
            {
                if (Items.Count == 0)
                    sb.Append(" { }");
                else
                {
                    sb.Append(" { ");
                    sb.Append(Items.Select(p => p.ToString()).JoinString(", "));
                    sb.Append(" }");
                }
            }
            return sb.ToString();
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsStackAllocExpression : CsExpression
    {
        public CsTypeName Type;
        public CsExpression SizeExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return string.Concat("stackalloc ", Type, '[', SizeExpression, ']');
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public abstract class CsLambdaExpression : CsExpression
    {
        public List<CsParameter> Parameters = new List<CsParameter>();
        protected StringBuilder parametersCs()
        {
            var sb = new StringBuilder();
            if (Parameters.Count == 1 && Parameters[0].Type == null)
                sb.Append(Parameters[0].ToString());
            else
            {
                sb.Append('(');
                sb.Append(Parameters.Select(p => p.ToString()).JoinString(", "));
                sb.Append(')');
            }
            sb.Append(" =>");
            return sb;
        }
    }
    public sealed class CsSimpleLambdaExpression : CsLambdaExpression
    {
        public CsExpression Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(parametersCs(), ' ', Body.ToString()); }
        private bool isImplicit()
        {
            return Parameters.Count > 0 && Parameters[0].Type == null;
        }
        internal override ResolveContext ToResolveContext(NameResolver resolver, bool isChecked)
        {
            // If parameter types are not specified, cannot turn into expression yet
            if (isImplicit())
                return new ResolveContextLambda(this);
            return new ResolveContextExpression(ToLinqExpression(resolver, isChecked), wasAnonymousFunction: true);
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked)
        {
            if (isImplicit())
                throw new InvalidOperationException("The implicitly-typed lambda expression “{0}” cannot be translated to a LINQ expression without knowing the types of its parameters. Use the ToLinqExpression(NameResolver,Type[]) overload to specify the parameter types.".Fmt(ToString()));

            var prmTypes = new Type[Parameters.Count];
            for (int i = 0; i < Parameters.Count; i++)
                prmTypes[i] = resolver.ResolveType(Parameters[i].Type);
            return ToLinqExpression(resolver, prmTypes, isChecked);
        }
        public Expression ToLinqExpression(NameResolver resolver, Type[] parameterTypes, bool isChecked)
        {
            if (parameterTypes.Length != Parameters.Count)
                throw new ArgumentException("Number of supplied parameter types does not match number of parameters on the lambda.");

            var prmExprs = new ParameterExpression[Parameters.Count];
            for (int i = 0; i < Parameters.Count; i++)
            {
                prmExprs[i] = Expression.Parameter(parameterTypes[i], Parameters[i].Name);
                resolver.AddLocalName(Parameters[i].Name, prmExprs[i]);
            }

            var body = Body.ToLinqExpression(resolver, isChecked);
            var lambda = Expression.Lambda(body, prmExprs);

            for (int i = 0; i < Parameters.Count; i++)
                resolver.ForgetLocalName(Parameters[i].Name);

            return lambda;
        }
    }
    public sealed class CsBlockLambdaExpression : CsLambdaExpression
    {
        public CsBlock Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(parametersCs(), '\n', Body.ToString().Trim()); }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsAnonymousMethodExpression : CsExpression
    {
        public List<CsParameter> Parameters;
        public CsBlock Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Parameters == null)
                return "delegate\n" + Body.ToString().Trim();
            else
                return string.Concat("delegate(", Parameters.Select(p => p.ToString()).JoinString(", "), ")\n", Body.ToString().Trim());
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsArrayLiteralExpression : CsExpression
    {
        public List<CsExpression> Expressions = new List<CsExpression>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (!Expressions.Any())
                return "{ }";
            var sb = new StringBuilder();
            sb.Append("{ ");
            bool first = true;
            foreach (var expr in Expressions)
            {
                if (!first)
                    sb.Append(", ");
                sb.Append(expr.ToString());
                first = false;
            }
            sb.Append(" }");
            return sb.ToString();
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public sealed class CsLinqExpression : CsExpression
    {
        public List<CsLinqElement> Elements = new List<CsLinqElement>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Elements[0]);
            foreach (var elem in Elements.Skip(1))
            {
                sb.Append('\n');
                sb.Append(elem.ToString().Indent());
            }
            return sb.ToString();
        }
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }
    public abstract class CsLinqElement : CsNode { }
    public sealed class CsLinqFromClause : CsLinqElement
    {
        public string ItemName;
        public CsExpression SourceExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("from ", ItemName.Sanitize(), " in ", SourceExpression.ToString()); }
    }
    public sealed class CsLinqJoinClause : CsLinqElement
    {
        public string ItemName;
        public CsExpression SourceExpression, KeyExpression1, KeyExpression2;
        public string IntoName;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var s = string.Concat("join ", ItemName.Sanitize(), " in ", SourceExpression, " on ", KeyExpression1, " equals ", KeyExpression2);
            return IntoName == null ? s : string.Concat(s, " into ", IntoName.Sanitize());
        }
    }
    public sealed class CsLinqLetClause : CsLinqElement
    {
        public string ItemName;
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("let ", ItemName.Sanitize(), " = ", Expression.ToString()); }
    }
    public sealed class CsLinqWhereClause : CsLinqElement
    {
        public CsExpression WhereExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("where ", WhereExpression.ToString()); }
    }
    public sealed class CsLinqOrderByClause : CsLinqElement
    {
        public List<CsLinqOrderBy> KeyExpressions = new List<CsLinqOrderBy>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return "orderby " + KeyExpressions.Select(k => k.ToString()).JoinString(", ");
        }
    }
    public enum LinqOrderByType { None, Ascending, Descending }
    public sealed class CsLinqOrderBy : CsNode
    {
        public CsExpression OrderByExpression;
        public LinqOrderByType OrderByType;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return OrderByExpression.ToString() + (OrderByType == LinqOrderByType.Ascending ? " ascending" : OrderByType == LinqOrderByType.Descending ? " descending" : "");
        }
    }
    public sealed class CsLinqSelectClause : CsLinqElement
    {
        public CsExpression SelectExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("select ", SelectExpression.ToString()); }
    }
    public sealed class CsLinqGroupByClause : CsLinqElement
    {
        public CsExpression SelectionExpression;
        public CsExpression KeyExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("group ", SelectionExpression.ToString(), " by ", KeyExpression.ToString()); }
    }
    public sealed class CsLinqIntoClause : CsLinqElement
    {
        public string ItemName;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("into ", ItemName.Sanitize()); }
    }
    #endregion

    #region Miscellaneous
    // CsNameAndExpression is used, for example, in field declarations:
    //      public string Button1 = "Abort", Button2 = "Retry", Button3 = "Ignore";
    public sealed class CsNameAndExpression : CsNode
    {
        public string Name;
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Expression == null)
                return Name.Sanitize();
            return string.Concat(Name.Sanitize(), " = ", Expression.ToString());
        }
    }

    public enum ArgumentMode { In, Out, Ref }
    public sealed class CsArgument : CsNode
    {
        public string Name;
        public ArgumentMode Mode;
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Name != null)
            {
                sb.Append(Name);
                sb.Append(": ");
            }
            if (Mode == ArgumentMode.Out)
                sb.Append("out ");
            else if (Mode == ArgumentMode.Ref)
                sb.Append("ref ");
            sb.Append(Expression.ToString());
            return sb.ToString();
        }
    }
    #endregion
}
