using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace RT.ParseCs
{
    /// <summary>Provides a base class for all nodes in the C# parse tree.</summary>
    public abstract class CsNode
    {
        /// <summary>The character index at which the node begins in the source code.</summary>
        public int StartIndex;
        /// <summary>The character index at which the node ends in the source code.</summary>
        public int EndIndex;

        /// <summary>Enumerates all the descendent nodes contained in this node.</summary>
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
                    sb.Append('\n');
                first = false;
                sb.Append(ns.ToString());
            }
            foreach (var ty in Types)
            {
                if (!first)
                    sb.Append('\n');
                first = false;
                sb.Append(ty.ToString());
            }

            return sb.ToString();
        }
    }
    /// <summary>
    ///     Base class for C# <c>using</c> declarations.</summary>
    /// <remarks>
    ///     Not to be confused with the <c>using (...) { ... }</c> <em>statement</em>; see <see cref="CsUsingStatement"/>.</remarks>
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
                    sb.Append('\n');
                first = false;
                sb.Append(ns.ToString().Indent());
            }
            foreach (var ty in Types)
            {
                if (!first)
                    sb.Append('\n');
                first = false;
                sb.Append(ty.ToString().Indent());
            }

            sb.Append("}\n");
            return sb.ToString();
        }
    }
    #endregion

    #region Members, except types
    /// <summary>Base class for C# members (field, property, event, method and type declarations).</summary>
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

    /// <summary>Base class for C# method and property declarations.</summary>
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

    /// <summary>C# property declaration (indexed properties are <see cref="CsIndexedProperty"/>, which is derived from this).</summary>
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
        /// <summary>
        ///     The constraints on each generic type parameter. <c>null</c> if there are no generic type parameters; empty
        ///     dictionary if generic but no constraints.</summary>
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
    /// <summary>Base class for C# type declarations (classes, structs, interfaces, enums and delegates).</summary>
    public abstract class CsType : CsMember
    {
        /// <summary>Name of the type.</summary>
        public string Name;
    }

    /// <summary>Base class for C# type declarations that can be generic (all except enums).</summary>
    public abstract class CsTypeCanBeGeneric : CsType
    {
        /// <summary>Generic type parameters if the type is generic; <c>null</c> otherwise.</summary>
        public List<CsGenericTypeParameter> GenericTypeParameters = null;

        /// <summary>
        ///     The constraints on each generic type parameter. <c>null</c> if there are no generic type parameters; empty
        ///     dictionary if generic but no constraints.</summary>
        public Dictionary<string, List<CsGenericTypeConstraint>> GenericTypeConstraints = null;

        /// <summary>Returns the generic type parameters as C# code in a string.</summary>
        protected string genericTypeParametersCs()
        {
            if (GenericTypeParameters == null)
                return "";
            return string.Concat("<", GenericTypeParameters.Select(g => g.ToString()).JoinString(", "), ">");
        }

        /// <summary>Returns the generic type constraints as C# code in a string.</summary>
        protected string genericTypeConstraintsCs()
        {
            if (GenericTypeConstraints == null)
                return "";
            return GenericTypeConstraints.Select(kvp => " where " + kvp.Key.Sanitize() + " : " + kvp.Value.Select(c => c.ToString()).JoinString(", ")).JoinString();
        }

        /// <summary>
        ///     Retrieves the parse tree nodes contained in all the generic type parameters (this includes their custom
        ///     attributes, if any).</summary>
        protected IEnumerable<CsNode> SubnodesGenericTypeParameters()
        {
            if (GenericTypeParameters != null)
                return GenericTypeParameters.SelectMany(gtp => gtp.Subnodes);
            return Enumerable.Empty<CsNode>();
        }

        /// <summary>Retrieves the parse tree nodes contained in all the generic type constraints.</summary>
        protected IEnumerable<CsNode> SubnodesGenericTypeConstraints()
        {
            if (GenericTypeConstraints != null)
                return GenericTypeConstraints.Values.SelectMany(gtcs => gtcs).SelectMany(gtc => gtc.Subnodes);
            return Enumerable.Empty<CsNode>();
        }
    }

    /// <summary>Base class for C# class, struct and interface declarations.</summary>
    public abstract class CsTypeLevel2 : CsTypeCanBeGeneric
    {
        /// <summary>Specifies whether the type is explicitly marked <c>partial</c>.</summary>
        public bool IsPartial;
        /// <summary>Base type and implemented interfaces.</summary>
        public List<CsTypeName> BaseTypes = null;
        /// <summary>
        ///     The members included in this type declaration, including methods, fields, properties, events and nested types.</summary>
        public List<CsMember> Members = new List<CsMember>();
        /// <summary>Returns the string <c>"class"</c>, <c>"struct"</c> or <c>"interface"</c> depending on the kind of type.</summary>
        protected abstract string typeKindCs { get; }
        /// <summary>Returns a <see cref="StringBuilder"/> containing the modifiers.</summary>
        protected override sealed StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            moreModifiers(sb);
            if (IsPartial) sb.Append("partial ");
            return sb;
        }
        /// <summary>
        ///     Adds all modifiers not declared in <see cref="CsTypeLevel2"/> to the provided <see cref="StringBuilder"/>.</summary>
        /// <param name="sb">
        ///     The <see cref="StringBuilder"/> instance to modify.</param>
        protected virtual void moreModifiers(StringBuilder sb) { }

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(typeKindCs);
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

        /// <summary>Enumerates all the descendent nodes contained in this node.</summary>
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

    /// <summary>C# interface declaration.</summary>
    public sealed class CsInterface : CsTypeLevel2
    {
        /// <summary>Returns the string <c>"interface"</c>.</summary>
        protected override string typeKindCs { get { return "interface"; } }
    }
    /// <summary>C# struct declaration.</summary>
    public sealed class CsStruct : CsTypeLevel2
    {
        /// <summary>Returns the string <c>"struct"</c>.</summary>
        protected override string typeKindCs { get { return "struct"; } }
    }
    /// <summary>C# class declaration.</summary>
    public sealed class CsClass : CsTypeLevel2
    {
        /// <summary>Specifies whether the class is explicitly marked <c>abstract</c>.</summary>
        public bool IsAbstract;
        /// <summary>Specifies whether the class is explicitly marked <c>sealed</c>.</summary>
        public bool IsSealed;
        /// <summary>Specifies whether the class is explicitly marked <c>static</c>.</summary>
        public bool IsStatic;

        /// <summary>Returns the string <c>"class"</c>.</summary>
        protected override string typeKindCs { get { return "class"; } }
        /// <summary>
        ///     Adds all modifiers not declared in <see cref="CsTypeLevel2"/> to the provided <see cref="StringBuilder"/>.</summary>
        /// <param name="sb">
        ///     The <see cref="StringBuilder"/> instance to modify.</param>
        protected override void moreModifiers(StringBuilder sb)
        {
            if (IsAbstract) sb.Append("abstract ");
            if (IsSealed) sb.Append("sealed ");
            if (IsStatic) sb.Append("static ");
        }
    }

    /// <summary>C# delegate declaration.</summary>
    public sealed class CsDelegate : CsTypeCanBeGeneric
    {
        /// <summary>The return type of the delegate.</summary>
        public CsTypeName ReturnType;
        /// <summary>The parameters of the delegate.</summary>
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

        /// <summary>Enumerates all the descendent nodes contained in this node.</summary>
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

    /// <summary>C# enum declaration.</summary>
    public sealed class CsEnum : CsType
    {
        /// <summary>The base numeric type of this enum.</summary>
        public CsTypeName BaseType;
        /// <summary>The enum values declared in this enum.</summary>
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

    /// <summary>C# declaration of a value in an enum.</summary>
    public sealed class CsEnumValue : CsNode
    {
        /// <summary>Custom attributes on this enum value.</summary>
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        /// <summary>Name of the enum value.</summary>
        public string Name;
        /// <summary>The value of the enum value (for example, in <c>None = 0</c>, this is the <c>0</c>).</summary>
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
    /// <summary>
    ///     C# parameter declaration.</summary>
    /// <remarks>
    ///     This is used in <see cref="CsAnonymousMethodExpression"/>, <see cref="CsBinaryOperatorOverload"/>, <see
    ///     cref="CsConstructor"/>, <see cref="CsDelegate"/> <see cref="CsIndexedProperty"/>, <see
    ///     cref="CsLambdaExpression"/>, <see cref="CsMethod"/> and <see cref="CsOperatorOverload"/>.</remarks>
    public sealed class CsParameter : CsNode
    {
        /// <summary>Custom attributes on this parameter.</summary>
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        /// <summary>
        ///     Type of the parameter. This may be <c>null</c> for a lambda expression in which the parameter types are not
        ///     explicitly specified.</summary>
        public CsTypeName Type;
        /// <summary>Name of the parameter.</summary>
        public string Name;
        /// <summary>Default value of the parameter, or <c>null</c> if the parameter is not optional.</summary>
        public CsExpression DefaultValue;
        /// <summary>Specifies whether the parameter has the <c>this</c> keyword (making the method an extension method).</summary>
        public bool IsThis;
        /// <summary>Specifies whether this is an <c>out</c> parameter.</summary>
        public bool IsOut;
        /// <summary>Specifies whether this is a <c>ref</c> parameter.</summary>
        public bool IsRef;
        /// <summary>Specifies whether this parameter has the <c>params</c> keyword.</summary>
        public bool IsParams;

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

    /// <summary>
    ///     Base type for places in which an identifier, optionally followed by generic type arguments, or a keyword can be
    ///     used.</summary>
    /// <remarks>
    ///     Used by <see cref="CsConcreteTypeName"/> (which covers things like <c>string</c> and <c>System.DateTime</c>), <see
    ///     cref="CsMemberAccessExpression"/> (for example, the <c>.Dispose</c> in <c>myObj.Dispose()</c>) and <see
    ///     cref="CsIdentifierExpression"/> (this includes references to variables or method groups inside expressions).</remarks>
    public abstract class CsIdentifier : CsNode
    {
        /// <summary>Determines whether this identifier has generic type arguments attached to it.</summary>
        public abstract bool EndsWithGenerics { get; }
    }
    /// <summary>C# identifier (<see cref="CsIdentifier"/>) that is not a keyword.</summary>
    public sealed class CsNameIdentifier : CsIdentifier
    {
        /// <summary>The name of which the identifier consists.</summary>
        public string Name;
        /// <summary>The generic type arguments attached to the identifier, or <c>null</c> if none.</summary>
        public List<CsTypeName> GenericTypeArguments = null;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return GenericTypeArguments == null ? Name.Sanitize() : string.Concat(Name.Sanitize(), '<', GenericTypeArguments.Select(p => p.ToString()).JoinString(", "), '>'); }
        /// <summary>Determines whether this identifier has generic type arguments attached to it.</summary>
        public override bool EndsWithGenerics { get { return GenericTypeArguments != null && GenericTypeArguments.Count > 0; } }
    }
    /// <summary>
    ///     C# identifier (<see cref="CsIdentifier"/>) that consists of a keyword (including <c>int</c>, <c>string</c>,
    ///     <c>void</c>, <c>base</c> etc.).</summary>
    public sealed class CsKeywordIdentifier : CsIdentifier
    {
        /// <summary>The keyword of which the identifier consists.</summary>
        public string Keyword;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Keyword; }
        /// <summary>Returns <c>false</c>. Keywords can never be followed by generic type arguments.</summary>
        public override bool EndsWithGenerics { get { return false; } }
    }

    /// <summary>C# reference to a type (including array types, generic type instantiations, etc.).</summary>
    public abstract class CsTypeName : CsNode
    {
        /// <summary>
        ///     If this type reference consists of a single identifier (no dots, no keywords, no generic type arguments and no
        ///     <c>global::</c> prefix), returns that identifier as a string. Otherwise, returns <c>null</c>.</summary>
        public virtual string GetSingleIdentifier() { return null; }
    }
    /// <summary>Placeholder for omitted generic type arguments in expressions like <c>typeof(Dictionary&lt;,&gt;)</c>.</summary>
    public sealed class CsEmptyGenericParameter : CsTypeName
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return ""; }
    }
    /// <summary>C# reference to a type by name (including possible namespaces, generic type arguments and nested types).</summary>
    public sealed class CsConcreteTypeName : CsTypeName
    {
        /// <summary>Specifies whether the type reference is prefixed by <c>global::</c>.</summary>
        public bool HasGlobal;
        /// <summary>
        ///     The parts of the type name that are separated by dots (<c>.</c>). For example, in
        ///     <c>System.Collections.Generic.List&lt;string&gt;.Enumerator</c>, the parts are: <c>Sytem</c>,
        ///     <c>Collections</c>, <c>Generic</c>, <c>List&lt;string&gt;</c>, and <c>Enumerator</c>.</summary>
        /// <remarks>
        ///     This does not tell you which parts of the name are namespaces and which are outer types. You need a <see
        ///     cref="NameResolver"/> to determine that.</remarks>
        public List<CsIdentifier> Parts = new List<CsIdentifier>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return (HasGlobal ? "global::" : "") + Parts.Select(p => p.ToString()).JoinString("."); }
        /// <summary>
        ///     If this type reference consists of a single identifier (no dots, no keywords, no generic type arguments and no
        ///     <c>global::</c> prefix), returns that identifier as a string. Otherwise, returns <c>null</c>.</summary>
        public override string GetSingleIdentifier()
        {
            if (HasGlobal || Parts.Count != 1)
                return null;
            var identifier = Parts[0] as CsNameIdentifier;
            return identifier != null && identifier.GenericTypeArguments == null ? identifier.Name : null;
        }
    }
    /// <summary>
    ///     C# array type reference (for example, <c>System.DateTime[]</c> or <c>string[,]</c>), including jagged arrays (such
    ///     as <c>int[][]</c>).</summary>
    public sealed class CsArrayTypeName : CsTypeName
    {
        /// <summary>
        ///     The element type of the array.</summary>
        /// <example>
        ///     In the case of <c>string[]</c>, this would be <c>string</c>.</example>
        public CsTypeName InnerType;
        /// <summary>
        ///     The ranks of each level of the array.</summary>
        /// <example>
        ///     <list type="bullet">
        ///         <item><description>
        ///             In the case of <c>string[]</c>, this is <c>{ 1 }</c>.</description></item>
        ///         <item><description>
        ///             In the case of <c>string[][]</c>, this is <c>{ 1, 1 }</c>.</description></item>
        ///         <item><description>
        ///             In the case of <c>string[,]</c>, this is <c>{ 2 }</c>.</description></item>
        ///         <item><description>
        ///             In the case of <c>string[,][]</c>, this is <c>{ 2, 1 }</c>.</description></item></list></example>
        public List<int> ArrayRanks = new List<int> { 1 };
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return InnerType.ToString() + ArrayRanks.Select(rank => string.Concat("[", new string(',', rank - 1), "]")).JoinString(); }
    }
    /// <summary>C# pointer type reference (for example, <c>int*</c> or <c>void*</c>).</summary>
    public sealed class CsPointerTypeName : CsTypeName
    {
        /// <summary>
        ///     The element type of the pointer.</summary>
        /// <example>
        ///     In the case of <c>int*</c>, this is <c>int</c>.</example>
        public CsTypeName InnerType;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return InnerType.ToString() + "*"; }
    }
    /// <summary>C# nullable type reference (for example, <c>int?</c>).</summary>
    public sealed class CsNullableTypeName : CsTypeName
    {
        /// <summary>
        ///     The element type of the nullable type.</summary>
        /// <example>
        ///     In the case of <c>int?</c>, this is <c>int</c>.</example>
        public CsTypeName InnerType;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return InnerType.ToString() + "?"; }
    }
    #endregion

    #region Generics
    /// <summary>Specifies whether a generic type parameter is covariant, contravariant, or neither.</summary>
    public enum VarianceMode
    {
        /// <summary>The generic type is neither covariant nor contravariant.</summary>
        Invariant,
        /// <summary>The generic type is covariant.</summary>
        Covariant,
        /// <summary>The generic type is contravariant.</summary>
        Contravariant
    }

    /// <summary>
    ///     C# generic type parameter declaration (for example, the <c>T</c> in <c>class List&lt;T&gt; { /* ... */ }</c>).</summary>
    public sealed class CsGenericTypeParameter : CsNode
    {
        /// <summary>Specifies whether the generic type parameter is covariant, contravariant, or neither.</summary>
        public VarianceMode Variance;
        /// <summary>The name of the generic type parameter.</summary>
        public string Name;
        /// <summary>The custom attributes of the generic type parameter.</summary>
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return CustomAttributes.Select(c => c.ToString() + ' ').JoinString() + (Variance == VarianceMode.Covariant ? "out " : Variance == VarianceMode.Contravariant ? "in " : "") + Name.Sanitize();
        }
    }

    /// <summary>Base class for C# generic type constraints.</summary>
    public abstract class CsGenericTypeConstraint : CsNode { }
    /// <summary>C# generic type constraint requiring a default constructor (<c>new()</c>).</summary>
    public sealed class CsGenericTypeConstraintNew : CsGenericTypeConstraint
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "new()"; }
    }
    /// <summary>C# generic type constraint requiring a reference type (<c>class</c>).</summary>
    public sealed class CsGenericTypeConstraintClass : CsGenericTypeConstraint
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "class"; }
    }
    /// <summary>C# generic type constraint requiring a value type (<c>struct</c>).</summary>
    public sealed class CsGenericTypeConstraintStruct : CsGenericTypeConstraint
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "struct"; }
    }
    /// <summary>C# generic type constraint requiring a base type or interface.</summary>
    public sealed class CsGenericTypeConstraintBaseClass : CsGenericTypeConstraint
    {
        /// <summary>The base type or interface required by the constraint.</summary>
        public CsTypeName BaseClass;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return BaseClass.ToString(); }
    }
    #endregion

    #region Custom attributes
    /// <summary>C# custom attribute.</summary>
    public sealed class CsCustomAttribute : CsNode
    {
        /// <summary>The type of the custom attribute.</summary>
        public CsTypeName Type;
        /// <summary>
        ///     The arguments of the custom attribute constructor call, not including property setters.</summary>
        /// <example>
        ///     In <c>[Foo("Name", ignoreCase: true, Validate = true)]</c>, this includes <c>"Name"</c> (a positional
        ///     argument) and <c>ignoreCase: true</c> (a named argument), but not the rest.</example>
        public List<CsArgument> Arguments = new List<CsArgument>();
        /// <summary>
        ///     The property setters in the custom attribute.</summary>
        /// <example>
        ///     In <c>[Foo("Name", ignoreCase: true, Validate = true)]</c>, this includes <c>Validate = true</c>.</example>
        public List<CsNameAndExpression> PropertySetters = new List<CsNameAndExpression>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Arguments.Count + PropertySetters.Count == 0)
                return Type.ToString();
            return string.Concat(Type.ToString(), '(', Arguments.Concat<CsNode>(PropertySetters).Select(p => p.ToString()).JoinString(", "), ')');
        }
    }

    /// <summary>Specifies the prefix that determines a custom attribute’s applicability.</summary>
    public enum CustomAttributeLocation
    {
        /// <summary>The custom attribute has no explicit prefix.</summary>
        None,
        /// <summary>The custom attribute is explicitly marked with the <c>assembly:</c> prefix.</summary>
        Assembly,
        /// <summary>The custom attribute is explicitly marked with the <c>module:</c> prefix.</summary>
        Module,
        /// <summary>The custom attribute is explicitly marked with the <c>type:</c> prefix.</summary>
        Type,
        /// <summary>The custom attribute is explicitly marked with the <c>method:</c> prefix.</summary>
        Method,
        /// <summary>The custom attribute is explicitly marked with the <c>property:</c> prefix.</summary>
        Property,
        /// <summary>The custom attribute is explicitly marked with the <c>field:</c> prefix.</summary>
        Field,
        /// <summary>The custom attribute is explicitly marked with the <c>event:</c> prefix.</summary>
        Event,
        /// <summary>The custom attribute is explicitly marked with the <c>param:</c> prefix.</summary>
        Param,
        /// <summary>The custom attribute is explicitly marked with the <c>return:</c> prefix.</summary>
        Return,
        /// <summary>The custom attribute is explicitly marked with the <c>typevar:</c> prefix.</summary>
        Typevar
    }

    /// <summary>C# custom attribute group (for example, <c>[Foo("Name"), Bar]</c>).</summary>
    public sealed class CsCustomAttributeGroup : CsNode
    {
        /// <summary>Specifies the prefix that determines the custom attribute’s applicability.</summary>
        public CustomAttributeLocation Location;
        /// <summary>The custom attributes contained in the group.</summary>
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
    /// <summary>Base class for C# statements.</summary>
    public abstract class CsStatement : CsNode
    {
        /// <summary>The goto labels attached to this statement.</summary>
        public List<string> GotoLabels;
        /// <summary>Returns the goto labels as a single string.</summary>
        protected string gotoLabels() { return GotoLabels == null ? "" : GotoLabels.Select(g => g.Sanitize() + ':').JoinString(" ") + (this is CsEmptyStatement ? ' ' : '\n'); }
    }

    /// <summary>C# empty statement.</summary>
    public sealed class CsEmptyStatement : CsStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return gotoLabels() + ";\n"; }
    }

    /// <summary>C# block statement (curly brackets containing further statements).</summary>
    public sealed class CsBlock : CsStatement
    {
        /// <summary>Statements contained in the block.</summary>
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

    /// <summary>Base class for <see cref="CsReturnStatement"/> and <see cref="CsThrowStatement"/>.</summary>
    public abstract class CsOptionalExpressionStatement : CsStatement
    {
        /// <summary>The expression that follows the keyword, or <c>null</c> if none.</summary>
        public CsExpression Expression;
        /// <summary>The keyword used by this expression.</summary>
        public abstract string Keyword { get; }
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Expression == null)
                return string.Concat(gotoLabels(), Keyword, ";\n");
            return string.Concat(gotoLabels(), Keyword, ' ', Expression.ToString(), ";\n");
        }
    }

    /// <summary>C# <c>return</c> statement.</summary>
    public sealed class CsReturnStatement : CsOptionalExpressionStatement
    {
        /// <summary>Returns <c>"return"</c>.</summary>
        public override string Keyword { get { return "return"; } }
    }

    /// <summary>C# <c>throw</c> statement.</summary>
    public sealed class CsThrowStatement : CsOptionalExpressionStatement
    {
        /// <summary>Returns <c>"throw"</c>.</summary>
        public override string Keyword { get { return "throw"; } }
    }

    /// <summary>
    ///     Base class for C# statements that require a curly bracket (<see cref="CsCheckedStatement"/>, <see
    ///     cref="CsUncheckedStatement"/> and <see cref="CsUnsafeStatement"/>).</summary>
    public abstract class CsBlockStatement : CsStatement
    {
        /// <summary>The block of which this statement consists.</summary>
        public CsBlock Block;
    }

    /// <summary>C# <c>checked</c> statement.</summary>
    public sealed class CsCheckedStatement : CsBlockStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "checked\n", Block.ToString()); }
    }

    /// <summary>C# <c>unchecked</c> statement.</summary>
    public sealed class CsUncheckedStatement : CsBlockStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "unchecked\n", Block.ToString()); }
    }

    /// <summary>C# <c>unsafe</c> statement.</summary>
    public sealed class CsUnsafeStatement : CsBlockStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "unsafe\n", Block.ToString()); }
    }

    /// <summary>C# <c>switch</c> statement.</summary>
    public sealed class CsSwitchStatement : CsStatement
    {
        /// <summary>The expression on which to switch.</summary>
        public CsExpression SwitchOn;
        /// <summary>The switch cases, including the <c>default</c> case.</summary>
        public List<CsSwitchCase> Cases = new List<CsSwitchCase>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "switch (", SwitchOn.ToString(), ")\n{\n", Cases.Select(c => c.ToString().Indent()).JoinString("\n"), "}\n"); }
    }

    /// <summary>Cases in a C# <c>switch</c> statement.</summary>
    public sealed class CsSwitchCase : CsNode
    {
        /// <summary>The expressions of each <c>case</c> label, or <c>null</c> for the <c>default</c> label.</summary>
        public List<CsExpression> CaseValues = new List<CsExpression>();
        /// <summary>The set of statements under this case label.</summary>
        public List<CsStatement> Statements;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(CaseValues.Select(c => c == null ? "default:\n" : "case " + c.ToString() + ":\n").JoinString(), Statements.Select(s => s.ToString().Indent()).JoinString()); }
    }

    /// <summary>C# statement consisting of an expression (including assignment and method calls).</summary>
    public sealed class CsExpressionStatement : CsStatement
    {
        /// <summary>The expression contained in this statement.</summary>
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), Expression.ToString(), ";\n"); }
    }

    /// <summary>C# variable declaration statement.</summary>
    public sealed class CsVariableDeclarationStatement : CsStatement
    {
        /// <summary>
        ///     The type of the variable(s) declared. If this is <c>var</c>, a compliant compiler must first check whether a
        ///     type named <c>var</c> is in scope before assuming that it is an implicitly-typed variable declaration.</summary>
        public CsTypeName Type;
        /// <summary>The variable names and their initialization expressions.</summary>
        public List<CsNameAndExpression> NamesAndInitializers = new List<CsNameAndExpression>();
        /// <summary>Specifies whether the variable is explicitly marked <c>const</c>.</summary>
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

    /// <summary>C# <c>foreach</c> statement.</summary>
    public sealed class CsForeachStatement : CsStatement
    {
        /// <summary>Type of the loop variable. (In a <c>foreach</c> loop, this is not optional.)</summary>
        public CsTypeName VariableType;
        /// <summary>Name of the loop variable.</summary>
        public string VariableName;
        /// <summary>The expression over whose result the <c>foreach</c> loop iterates.</summary>
        public CsExpression LoopExpression;
        /// <summary>The body of the loop.</summary>
        public CsStatement Body;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "foreach (", VariableType == null ? "" : VariableType.ToString() + ' ', VariableName.Sanitize(), " in ", LoopExpression.ToString(), ")\n", Body is CsBlock ? Body.ToString() : Body.ToString().Indent()); }
    }

    /// <summary>C# <c>for</c> statement.</summary>
    public sealed class CsForStatement : CsStatement
    {
        /// <summary>
        ///     The initialization statement(s).</summary>
        /// <example>
        ///     In <c>for (i = 0, j = 0; i &lt; length; i++, j++) { /* ... */ }</c>, these are the two statements <c>i =0</c>
        ///     and <c>j = 0</c>.</example>
        public List<CsStatement> InitializationStatements = new List<CsStatement>();
        /// <summary>
        ///     The termination condition.</summary>
        /// <example>
        ///     In <c>for (i = 0, j = 0; i &lt; length; i++, j++) { /* ... */ }</c>, this is the expression <c>i &lt;
        ///     length</c>.</example>
        public CsExpression TerminationCondition;
        /// <summary>
        ///     The loop iteration expression(s).</summary>
        /// <example>
        ///     In <c>for (i = 0, j = 0; i &lt; length; i++, j++) { /* ... */ }</c>, these are the two expressions <c>i++</c>
        ///     and <c>j++</c>.</example>
        public List<CsExpression> LoopExpressions = new List<CsExpression>();
        /// <summary>The body of the loop.</summary>
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

    /// <summary>
    ///     C# <c>using</c> statement.</summary>
    /// <remarks>
    ///     Not to be confused with the <c>using</c> <em>declaration</em>; see <see cref="CsUsing"/>.</remarks>
    public sealed class CsUsingStatement : CsStatement
    {
        /// <summary>The initialization statement (either a variable declaration or an expression statement).</summary>
        public CsStatement InitializationStatement;
        /// <summary>The body of the statement.</summary>
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

    /// <summary>C# <c>fixed</c> statement.</summary>
    public sealed class CsFixedStatement : CsStatement
    {
        /// <summary>The initialization statement (either a variable declaration or an expression statement).</summary>
        public CsStatement InitializationStatement;
        /// <summary>The body of the statement.</summary>
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

    /// <summary>C# <c>if</c> statement.</summary>
    public sealed class CsIfStatement : CsStatement
    {
        /// <summary>The condition.</summary>
        public CsExpression IfExpression;
        /// <summary>The first body of the statement.</summary>
        public CsStatement Statement;
        /// <summary>The else body, or <c>null</c> if none.</summary>
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

    /// <summary>
    ///     Base class for C# statements that consist of a keyword, an expression and a body (<c>while</c> and <c>lock</c>).</summary>
    public abstract class CsExpressionBlockStatement : CsStatement
    {
        /// <summary>The <c>while</c>/<c>lock</c> expression.</summary>
        public CsExpression Expression;
        /// <summary>The statement body.</summary>
        public CsStatement Body;
        /// <summary>The keyword of this statement (<c>while</c> and <c>lock</c>).</summary>
        public abstract string Keyword { get; }
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), Keyword, " (", Expression.ToString(), ")\n", Body is CsBlock ? Body.ToString() : Body.ToString().Indent()); }
    }

    /// <summary>
    ///     C# <c>while</c> statement.</summary>
    /// <remarks>
    ///     Not to be confused with <see cref="CsDoWhileStatement"/>.</remarks>
    public sealed class CsWhileStatement : CsExpressionBlockStatement
    {
        /// <summary>Returns <c>"while"</c>.</summary>
        public override string Keyword { get { return "while"; } }
    }

    /// <summary>C# <c>lock</c> statement.</summary>
    public sealed class CsLockStatement : CsExpressionBlockStatement
    {
        /// <summary>Returns <c>"lock"</c>.</summary>
        public override string Keyword { get { return "lock"; } }
    }

    /// <summary>C# <c>do ... while</c> statement.</summary>
    public sealed class CsDoWhileStatement : CsStatement
    {
        /// <summary>The <c>while</c> expression at the end of the statement.</summary>
        public CsExpression WhileExpression;
        /// <summary>The statement body.</summary>
        public CsStatement Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "do\n", Body is CsBlock ? Body.ToString() : Body.ToString().Indent(), "while (", WhileExpression.ToString(), ");\n"); }
    }

    /// <summary>C# <c>try ... catch ... finally</c> statement.</summary>
    public sealed class CsTryStatement : CsStatement
    {
        /// <summary>The body of the <c>try</c> block.</summary>
        public CsBlock TryBody;
        /// <summary>The <c>catch</c> clauses (may be empty).</summary>
        public List<CsCatchClause> Catches = new List<CsCatchClause>();
        /// <summary>The body of the <c>finally</c> clause, or <c>null</c> if there is no <c>finally</c> clause.</summary>
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

    /// <summary>C# <c>catch</c> clause in a <see cref="CsTryStatement"/>.</summary>
    public sealed class CsCatchClause : CsNode
    {
        /// <summary>The type of exception to catch (or <c>null</c> if no exception type is specified).</summary>
        public CsTypeName Type;
        /// <summary>The name of the variable to contain the exception (or <c>null</c> if no variable name is provided).</summary>
        public string Name;
        /// <summary>The body of the catch clause.</summary>
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
            sb.Append('\n');
            sb.Append(Body is CsBlock ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }

    /// <summary>C# <c>goto</c> statement.</summary>
    public sealed class CsGotoStatement : CsStatement
    {
        /// <summary>The goto label to jump to.</summary>
        public string Label;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "goto ", Label.Sanitize(), ";\n"); }
    }

    /// <summary>C# <c>continue</c> statement.</summary>
    public sealed class CsContinueStatement : CsStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "continue;\n"); }
    }

    /// <summary>C# <c>break</c> statement.</summary>
    public sealed class CsBreakStatement : CsStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "break;\n"); }
    }

    /// <summary>C# <c>goto default</c> statement (for use inside <see cref="CsSwitchStatement"/>).</summary>
    public sealed class CsGotoDefaultStatement : CsStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "goto default;\n"); }
    }

    /// <summary>C# <c>goto case</c> statement (for use inside <see cref="CsSwitchStatement"/>).</summary>
    public sealed class CsGotoCaseStatement : CsStatement
    {
        /// <summary>The case expression.</summary>
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "goto case ", Expression.ToString(), ";\n"); }
    }

    /// <summary>C# <c>yield break</c> statement.</summary>
    public sealed class CsYieldBreakStatement : CsStatement
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "yield break;\n"); }
    }

    /// <summary>C# <c>yield return</c> statement.</summary>
    public sealed class CsYieldReturnStatement : CsStatement
    {
        /// <summary>The expression to yield.</summary>
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(gotoLabels(), "yield return ", Expression.ToString(), ";\n"); }
    }
    #endregion

    #region Expressions
    /// <summary>Base class for C# expressions.</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# conditional expression (ternary operator).</summary>
    public sealed class CsConditionalExpression : CsExpression
    {
        /// <summary>The condition (the expression before <c>?</c>).</summary>
        public CsExpression Condition;
        /// <summary>The true part (the expression between <c>?</c> and <c>:</c>).</summary>
        public CsExpression TruePart;
        /// <summary>The false part (the expression after <c>:</c>).</summary>
        public CsExpression FalsePart;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return string.Concat(Condition.ToString(), " ? ", TruePart.ToString(), " : ", FalsePart.ToString());
        }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>
    ///     Specifies a binary operator used in a <see cref="CsBinaryOperatorExpression"/> or a <see
    ///     cref="CsBinaryOperatorOverload"/>.</summary>
    public enum BinaryOperator
    {
        #region Operators used both in CsBinaryOperatorExpression and in CsBinaryOperatorOverload
        /// <summary>
        ///     The <c>*</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Times,
        /// <summary>
        ///     The <c>/</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Div,
        /// <summary>
        ///     The <c>%</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Mod,
        /// <summary>
        ///     The <c>+</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Plus,
        /// <summary>
        ///     The <c>-</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Minus,
        /// <summary>
        ///     The <c>&lt;&lt;</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Shl,
        /// <summary>
        ///     The <c>&gt;&gt;</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Shr,
        /// <summary>
        ///     The <c>&lt;</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Less,
        /// <summary>
        ///     The <c>&gt;</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Greater,
        /// <summary>
        ///     The <c>&lt;=</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        LessEq,
        /// <summary>
        ///     The <c>&gt;=</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        GreaterEq,
        /// <summary>
        ///     The <c>==</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Eq,
        /// <summary>
        ///     The <c>!=</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        NotEq,
        /// <summary>
        ///     The <c>&amp;</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        And,
        /// <summary>
        ///     The <c>^</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Xor,
        /// <summary>
        ///     The <c>|</c> operator. Used both in <see cref="CsBinaryOperatorExpression"/> and in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Or,
        #endregion

        #region Operators used only in CsBinaryOperatorExpression
        /// <summary>
        ///     The <c>&amp;&amp;</c> operator. Used only in <see cref="CsBinaryOperatorExpression"/>, but not in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        AndAnd,
        /// <summary>
        ///     The <c>||</c> operator. Used only in <see cref="CsBinaryOperatorExpression"/>, but not in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        OrOr,
        /// <summary>
        ///     The <c>??</c> operator. Used only in <see cref="CsBinaryOperatorExpression"/>, but not in <see
        ///     cref="CsBinaryOperatorOverload"/>.</summary>
        Coalesce
        #endregion
    }

    /// <summary>C# binary operator expression (for example, <c>a + b</c>).</summary>
    public sealed class CsBinaryOperatorExpression : CsExpression
    {
        /// <summary>The specific operator used in this expression.</summary>
        public BinaryOperator Operator;
        /// <summary>The expression left of the operator.</summary>
        public CsExpression Left;
        /// <summary>The expression right of the operator.</summary>
        public CsExpression Right;

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(Left.ToString(), ' ', Operator.ToCs(), ' ', Right.ToString()); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
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

    /// <summary>Specifies a binary type operator used in a <see cref="CsBinaryTypeOperatorExpression"/>.</summary>
    public enum BinaryTypeOperator
    {
        /// <summary>The <c>is</c> operator.</summary>
        Is,
        /// <summary>The <c>as</c> operator.</summary>
        As
    }

    /// <summary>C# binary type operator expression (for example, <c>obj is string</c>).</summary>
    public sealed class CsBinaryTypeOperatorExpression : CsExpression
    {
        /// <summary>The specific type operator used in this expression.</summary>
        public BinaryTypeOperator Operator;
        /// <summary>The expression left of the operator.</summary>
        public CsExpression Left;
        /// <summary>The type name right of the operator.</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>
    ///     Specifies a unary operator used in a <see cref="CsUnaryOperatorExpression"/> or a <see
    ///     cref="CsUnaryOperatorOverload"/>.</summary>
    public enum UnaryOperator
    {
        #region Unary operators used both in CsUnaryOperatorExpression and in CsUnaryOperatorOverload
        /// <summary>
        ///     The <c>+</c> operator. Used both in <see cref="CsUnaryOperatorExpression"/> and in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        Plus,
        /// <summary>
        ///     The <c>-</c> operator. Used both in <see cref="CsUnaryOperatorExpression"/> and in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        Minus,
        /// <summary>
        ///     The <c>!</c> operator. Used both in <see cref="CsUnaryOperatorExpression"/> and in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        Not,
        /// <summary>
        ///     The <c>~</c> operator. Used both in <see cref="CsUnaryOperatorExpression"/> and in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        Neg,
        /// <summary>
        ///     The prefix <c>++</c> operator. Used both in <see cref="CsUnaryOperatorExpression"/> and in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        PrefixInc,
        /// <summary>
        ///     The prefix <c>--</c> operator. Used both in <see cref="CsUnaryOperatorExpression"/> and in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        PrefixDec,
        #endregion

        #region Unary operators used only in CsUnaryOperatorExpression
        /// <summary>
        ///     The postfix <c>++</c> operator. Used in <see cref="CsUnaryOperatorExpression"/>, but not in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        PostfixInc,
        /// <summary>
        ///     The postfix <c>--</c> operator. Used in <see cref="CsUnaryOperatorExpression"/>, but not in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        PostfixDec,
        /// <summary>
        ///     The <c>*</c> operator. Used in <see cref="CsUnaryOperatorExpression"/>, but not in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        PointerDeref,
        /// <summary>
        ///     The <c>&amp;</c> operator. Used in <see cref="CsUnaryOperatorExpression"/>, but not in <see
        ///     cref="CsUnaryOperatorOverload"/>.</summary>
        AddressOf,
        #endregion

        #region Unary operators used only in CsUnaryOperatorOverload
        /// <summary>
        ///     The overloadable operator <c>true</c>. Used in <see cref="CsUnaryOperatorOverload"/>, but not in <see
        ///     cref="CsUnaryOperatorExpression"/>.</summary>
        True,
        /// <summary>
        ///     The overloadable operator <c>false</c>. Used in <see cref="CsUnaryOperatorOverload"/>, but not in <see
        ///     cref="CsUnaryOperatorExpression"/>.</summary>
        False
        #endregion
    }

    /// <summary>C# unary operator expression (for example, <c>-a</c>).</summary>
    public sealed class CsUnaryOperatorExpression : CsExpression
    {
        /// <summary>The specific operator used in this expression.</summary>
        public UnaryOperator Operator;
        /// <summary>The operand (the expression after the operator).</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked)
        {
            // Special case: if this is a unary minus operator that operates on a CsNumberLiteralExpression,
            // simply add the "-" and parse the number directly
            if (Operator == UnaryOperator.Minus && Operand is CsNumberLiteralExpression)
                return Expression.Constant(ParserUtil.ParseNumericLiteral("-" + ((CsNumberLiteralExpression) Operand).Literal));
            throw new NotImplementedException();
        }
    }

    /// <summary>C# cast expression (for example, <c>(string) obj</c>).</summary>
    public sealed class CsCastExpression : CsExpression
    {
        /// <summary>The type being cast to.</summary>
        public CsTypeName Type;
        /// <summary>The operand (the expression being cast).</summary>
        public CsExpression Operand;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat('(', Type.ToString(), ") ", Operand.ToString()); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked)
        {
            var operand = ToResolveContext(Operand, resolver, isChecked);
            var type = resolver.ResolveType(Type);
            if (operand is ResolveContextLambda)
                throw new NotImplementedException();
            return Expression.Convert(operand.ToExpression(), type);
        }
    }

    /// <summary>Specifies the type of member access (<c>.</c> or <c>-&gt;</c>).</summary>
    public enum MemberAccessType
    {
        /// <summary>Regular member access (<c>.</c>).</summary>
        Regular,
        /// <summary>Pointer-dereferencing member access (<c>-&gt;</c>).</summary>
        PointerDeref
    }

    /// <summary>
    ///     C# member access expression (for example, <c>str.Length</c>).</summary>
    /// <remarks>
    ///     This includes access to instance methods. For example, <c>str.Substring(index)</c> is a <see
    ///     cref="CsFunctionCallExpression"/> in which the <see cref="CsFunctionCallExpression.Left"/> operand is a <see
    ///     cref="CsMemberAccessExpression"/>.</remarks>
    public sealed class CsMemberAccessExpression : CsExpression
    {
        /// <summary>Specifies the type of member access (<c>.</c> or <c>-&gt;</c>).</summary>
        public MemberAccessType AccessType;
        /// <summary>The expression left of the member access.</summary>
        public CsExpression Left;
        /// <summary>The identifier referring to the member being accessed.</summary>
        public CsIdentifier Right;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(Left.ToString(), AccessType == MemberAccessType.PointerDeref ? "->" : ".", Right.ToString()); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return ToResolveContext(resolver, isChecked).ToExpression(); }
        internal override ResolveContext ToResolveContext(NameResolver resolver, bool isChecked)
        {
            if (AccessType == MemberAccessType.PointerDeref)
                throw new InvalidOperationException("Pointer dereference is not supported in LINQ expressions.");
            return resolver.ResolveSimpleName(Right, ToResolveContext(Left, resolver, isChecked));
        }
    }

    /// <summary>C# function invocation expression, including indexers (for example, <c>foo(5)</c> and <c>foo[5]</c>).</summary>
    public sealed class CsFunctionCallExpression : CsExpression
    {
        /// <summary>Specifies whether this expression invokes a function or an indexer.</summary>
        public bool IsIndexer;
        /// <summary>The expression left of the invocation.</summary>
        public CsExpression Left;
        /// <summary>The arguments of the invocation.</summary>
        public List<CsArgument> Arguments = new List<CsArgument>();

        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(Left.ToString(), IsIndexer ? '[' : '(', Arguments.Select(p => p.ToString()).JoinString(", "), IsIndexer ? ']' : ')'); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
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

    /// <summary>
    ///     Base class of C# unary type operator expressions (<see cref="CsDefaultExpression"/>, <see
    ///     cref="CsSizeofExpression"/> and <see cref="CsTypeofExpression"/>).</summary>
    public abstract class CsTypeOperatorExpression : CsExpression
    {
        /// <summary>The type referred to in the type operator.</summary>
        public CsTypeName Type;
    }

    /// <summary>C# <c>typeof(T)</c> expression.</summary>
    public sealed class CsTypeofExpression : CsTypeOperatorExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("typeof(", Type.ToString(), ')'); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>sizeof(T)</c> expression.</summary>
    public sealed class CsSizeofExpression : CsTypeOperatorExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("sizeof(", Type.ToString(), ')'); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>
    ///     C# <c>default(T)</c> expression.</summary>
    /// <remarks>
    ///     Not to be confused with <c>default</c> labels in switch statements; see <see cref="CsSwitchCase"/>.</remarks>
    public sealed class CsDefaultExpression : CsTypeOperatorExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("default(", Type.ToString(), ')'); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>Base class for C# <c>checked</c> and <c>unchecked</c> expressions.</summary>
    public abstract class CsCheckedUncheckedExpression : CsExpression
    {
        /// <summary>The expression contained in this <c>checked</c> or <c>unchecked</c> expression.</summary>
        public CsExpression Subexpression;
    }

    /// <summary>C# <c>checked</c> expression.</summary>
    public sealed class CsCheckedExpression : CsCheckedUncheckedExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("checked(", Subexpression.ToString(), ')'); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>unchecked</c> expression.</summary>
    public sealed class CsUncheckedExpression : CsCheckedUncheckedExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("unchecked(", Subexpression.ToString(), ')'); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# expression containing a reference to a variable (a local, parameter, field, property, event, etc.).</summary>
    public sealed class CsIdentifierExpression : CsExpression
    {
        /// <summary>The identifier referred to.</summary>
        public CsIdentifier Identifier;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Identifier.ToString(); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return ToResolveContext(resolver, isChecked).ToExpression(); }
        internal override ResolveContext ToResolveContext(NameResolver resolver, bool isChecked) { return resolver.ResolveSimpleName(Identifier); }
    }

    /// <summary>C# parenthesized expression.</summary>
    public sealed class CsParenthesizedExpression : CsExpression
    {
        /// <summary>The expression contained in parentheses.</summary>
        public CsExpression Subexpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat('(', Subexpression.ToString(), ')'); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return Subexpression.ToLinqExpression(resolver, isChecked); }
    }

    /// <summary>C# expression consisting of a string literal.</summary>
    public sealed class CsStringLiteralExpression : CsExpression
    {
        /// <summary>The string encoded by this literal.</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return Expression.Constant(Literal); }
    }

    /// <summary>C# expression consisting of a character literal.</summary>
    public sealed class CsCharacterLiteralExpression : CsExpression
    {
        /// <summary>The character encoded by this literal.</summary>
        public char Literal;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat('\'', Literal.CsEscape(true, false), '\''); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# expression consisting of a numeric literal.</summary>
    public sealed class CsNumberLiteralExpression : CsExpression
    {
        /// <summary>
        ///     The numeric literal, literally. If the number was specified as hexadecimal, a float with exponential notation
        ///     or anything, this will still be reflected here.</summary>
        public string Literal;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Literal; }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { return Expression.Constant(ParserUtil.ParseNumericLiteral(Literal)); }
    }

    /// <summary>C# expression consisting of a boolean literal.</summary>
    public sealed class CsBooleanLiteralExpression : CsExpression
    {
        /// <summary>The boolean encoded by this literal.</summary>
        public bool Literal;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Literal ? "true" : "false"; }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>null</c> literal.</summary>
    public sealed class CsNullExpression : CsExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "null"; }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>this</c> expression.</summary>
    public sealed class CsThisExpression : CsExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "this"; }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>base</c> expression (legal only for base member access).</summary>
    public sealed class CsBaseExpression : CsExpression
    {
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return "base"; }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>new T()</c> expression (constructor invocation).</summary>
    public sealed class CsNewConstructorExpression : CsExpression
    {
        /// <summary>The type whose constructor is being invoked.</summary>
        public CsTypeName Type;
        /// <summary>Arguments to the constructor call.</summary>
        public List<CsArgument> Arguments = new List<CsArgument>();
        /// <summary>
        ///     List of expressions if the constructor call is followed by object or collection initialization syntax,
        ///     <c>null</c> if not.</summary>
        /// <remarks>
        ///     If object initialization syntax is used, the expressions are all assignment expressions.</remarks>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>new { ... }</c> expression (anonymous type construction).</summary>
    public sealed class CsNewAnonymousTypeExpression : CsExpression
    {
        /// <summary>
        ///     The list of initializers in this expression. Those that consist of a name and an initializer are represented
        ///     as assignment expressions.</summary>
        public List<CsExpression> Initializers = new List<CsExpression>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("new { ", Initializers.Select(ini => ini.ToString()).JoinString(", "), " }"); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>new[] { ... }</c> expression (implicitly-typed array).</summary>
    public sealed class CsNewImplicitlyTypedArrayExpression : CsExpression
    {
        /// <summary>The expressions listed in the array.</summary>
        public List<CsExpression> Items = new List<CsExpression>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return Items.Count == 0 ? "new[] { }" : string.Concat("new[] { ", Items.Select(p => p.ToString()).JoinString(", "), " }"); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>new T[num]</c> expression (array instantiation).</summary>
    public sealed class CsNewArrayExpression : CsExpression
    {
        /// <summary>The element type of the array (for example, in <c>new string[5]</c>, this is <c>string</c>).</summary>
        public CsTypeName Type;
        /// <summary>
        ///     The expressions used to compute the dimensions of the first level of arrays. For example, in <c>new
        ///     string[width, height][]</c>, this is <c>width</c> and <c>height</c>.</summary>
        public List<CsExpression> SizeExpressions = new List<CsExpression>();
        /// <summary>
        ///     The array ranks that follow the first level. For example, in <c>new string[width, height][]</c>, this is <c>{
        ///     1 }</c>.</summary>
        public List<int> AdditionalRanks = new List<int>();
        /// <summary>
        ///     The expressions used to populate the array (for example, <c>new string[1] { "Tom" }</c>), or <c>null</c> if no
        ///     such list is included.</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>
    ///     C# <c>stackalloc T[num]</c> expression (array declaration in unsafe code).</summary>
    /// <remarks>
    ///     <para>
    ///         This expression is only legal in a variable declaration for a pointer type, for example:</para>
    ///     <code>
    ///         int* block = stackalloc int[256];</code></remarks>
    public sealed class CsStackAllocExpression : CsExpression
    {
        /// <summary>The type of the stack-allocated array.</summary>
        public CsTypeName Type;
        /// <summary>The expression used to compute the size of the array.</summary>
        public CsExpression SizeExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return string.Concat("stackalloc ", Type, '[', SizeExpression, ']');
        }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>Base class for C# lambda expressions.</summary>
    public abstract class CsLambdaExpression : CsExpression
    {
        /// <summary>
        ///     The parameters of the lambda expression. If the parameters are implicitly typed, their types here are
        ///     <c>null</c>.</summary>
        public List<CsParameter> Parameters = new List<CsParameter>();

        /// <summary>
        ///     Returns a <see cref="StringBuilder"/> containing the parameters of the lambda expression, including the
        ///     parentheses and the <c>=&gt;</c>.</summary>
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

    /// <summary>C# lambda expression whose body is another expression.</summary>
    public sealed class CsSimpleLambdaExpression : CsLambdaExpression
    {
        /// <summary>The body of the lambda expression.</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked)
        {
            if (isImplicit())
                throw new InvalidOperationException("The implicitly-typed lambda expression “{0}” cannot be translated to a LINQ expression without knowing the types of its parameters. Use the ToLinqExpression(NameResolver,Type[]) overload to specify the parameter types.".Fmt(ToString()));

            var prmTypes = new Type[Parameters.Count];
            for (int i = 0; i < Parameters.Count; i++)
                prmTypes[i] = resolver.ResolveType(Parameters[i].Type);
            return ToLinqExpression(resolver, prmTypes, isChecked);
        }
        /// <summary>
        ///     See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        /// <param name="resolver">
        ///     See <see cref="CsExpression.ToLinqExpression"/>.</param>
        /// <param name="parameterTypes">
        ///     The types of the parameters of the lambda expression.</param>
        /// <param name="isChecked">
        ///     See <see cref="CsExpression.ToLinqExpression"/>.</param>
        /// <returns>
        ///     An expression deriving from <see cref="System.Linq.Expressions.Expression"/>.</returns>
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

    /// <summary>C# lambda expression whose body is a block.</summary>
    public sealed class CsBlockLambdaExpression : CsLambdaExpression
    {
        /// <summary>The body of the lambda expression.</summary>
        public CsBlock Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat(parametersCs(), '\n', Body.ToString().Trim()); }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# <c>delegate { ... }</c> expression (anonymous method).</summary>
    public sealed class CsAnonymousMethodExpression : CsExpression
    {
        /// <summary>The parameters of the anonymous method, or <c>null</c> if none are specified.</summary>
        public List<CsParameter> Parameters;
        /// <summary>The body of the anonymous method.</summary>
        public CsBlock Body;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Parameters == null)
                return "delegate\n" + Body.ToString().Trim();
            else
                return string.Concat("delegate(", Parameters.Select(p => p.ToString()).JoinString(", "), ")\n", Body.ToString().Trim());
        }
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>
    ///     C# array literal expression.</summary>
    /// <remarks>
    ///     <para>
    ///         This expression is not legal in most places. It can legally occur only in the following contexts:</para>
    ///     <list type="bullet">
    ///         <item><description>
    ///             <para>
    ///                 Array initialization in a variable declaration:</para>
    ///             <code>
    ///                 string[] arr = { "foo", "bar" };</code></description></item>
    ///         <item><description>
    ///             <para>
    ///                 Nested collection initializers:</para>
    ///             <code>
    ///                 var dic = new Dictionary&lt;string, int&gt; { { "one", 1 }, { "two", 2 } };</code></description></item>
    ///         <item><description>
    ///             <para>
    ///                 Collection initializer inside an object initializer:</para>
    ///             <code>
    ///                 var obj = new MyObject { MyList = { "one", "two" } };</code></description></item></list></remarks>
    public sealed class CsArrayLiteralExpression : CsExpression
    {
        /// <summary>The expressions contained in the array literal expression.</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>C# language-integrated query (LINQ) expression.</summary>
    public sealed class CsLinqExpression : CsExpression
    {
        /// <summary>The elements that make up this LINQ expression.</summary>
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
        /// <summary>See <see cref="CsExpression.ToLinqExpression"/>.</summary>
        public override Expression ToLinqExpression(NameResolver resolver, bool isChecked) { throw new NotImplementedException(); }
    }

    /// <summary>Base class for the building blocks that make up a <see cref="CsLinqExpression"/>.</summary>
    public abstract class CsLinqElement : CsNode { }

    /// <summary>C# LINQ <c>from ... in ...</c> clause.</summary>
    public sealed class CsLinqFromClause : CsLinqElement
    {
        /// <summary>The query variable name.</summary>
        public string ItemName;
        /// <summary>The expression that identifies the collection to iterate.</summary>
        public CsExpression SourceExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("from ", ItemName.Sanitize(), " in ", SourceExpression.ToString()); }
    }

    /// <summary>C# LINQ <c>join ... in ... on ... equals ... [into ...]</c> clause.</summary>
    public sealed class CsLinqJoinClause : CsLinqElement
    {
        /// <summary>The query variable name.</summary>
        public string ItemName;
        /// <summary>The expression identifying the collection to join to.</summary>
        public CsExpression SourceExpression;
        /// <summary>The expression left of the <c>equals</c>.</summary>
        public CsExpression KeyExpression1;
        /// <summary>The expression right of the <c>equals</c>.</summary>
        public CsExpression KeyExpression2;
        /// <summary>The name of the variable to contain the result, or <c>null</c> if none.</summary>
        public string IntoName;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            var s = string.Concat("join ", ItemName.Sanitize(), " in ", SourceExpression, " on ", KeyExpression1, " equals ", KeyExpression2);
            return IntoName == null ? s : string.Concat(s, " into ", IntoName.Sanitize());
        }
    }

    /// <summary>C# LINQ <c>let ... = ...</c> clause.</summary>
    public sealed class CsLinqLetClause : CsLinqElement
    {
        /// <summary>The let variable.</summary>
        public string ItemName;
        /// <summary>The expression to assign to the variable.</summary>
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("let ", ItemName.Sanitize(), " = ", Expression.ToString()); }
    }

    /// <summary>C# LINQ <c>where</c> clause.</summary>
    public sealed class CsLinqWhereClause : CsLinqElement
    {
        /// <summary>The filter expression.</summary>
        public CsExpression WhereExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("where ", WhereExpression.ToString()); }
    }

    /// <summary>C# LINQ <c>orderby</c> clause.</summary>
    public sealed class CsLinqOrderByClause : CsLinqElement
    {
        /// <summary>The order by elements, of which there may be several.</summary>
        public List<CsLinqOrderBy> KeyExpressions = new List<CsLinqOrderBy>();
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return "orderby " + KeyExpressions.Select(k => k.ToString()).JoinString(", ");
        }
    }

    /// <summary>Specifies the order direction in a C# LINQ <c>orderby</c> clause.</summary>
    public enum LinqOrderByType
    {
        /// <summary>The <c>orderby</c> clause has no explicit order direction.</summary>
        None,
        /// <summary>The <c>orderby</c> clause is explicitly marked <c>ascending</c>.</summary>
        Ascending,
        /// <summary>The <c>orderby</c> clause is explicitly marked <c>descending</c>.</summary>
        Descending
    }

    /// <summary>One of the elements in a C# LINQ <c>orderby</c> clause.</summary>
    public sealed class CsLinqOrderBy : CsNode
    {
        /// <summary>The expression to order by.</summary>
        public CsExpression OrderByExpression;
        /// <summary>The order direction.</summary>
        public LinqOrderByType OrderByType;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            return OrderByExpression.ToString() + (OrderByType == LinqOrderByType.Ascending ? " ascending" : OrderByType == LinqOrderByType.Descending ? " descending" : "");
        }
    }

    /// <summary>C# LINQ <c>select</c> clause.</summary>
    public sealed class CsLinqSelectClause : CsLinqElement
    {
        /// <summary>The expression to select.</summary>
        public CsExpression SelectExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("select ", SelectExpression.ToString()); }
    }

    /// <summary>C# LINQ <c>group ... by ...</c> clause.</summary>
    public sealed class CsLinqGroupByClause : CsLinqElement
    {
        /// <summary>The expression that returns the values to group.</summary>
        public CsExpression SelectionExpression;
        /// <summary>The expression that identifies the key to group by.</summary>
        public CsExpression KeyExpression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("group ", SelectionExpression.ToString(), " by ", KeyExpression.ToString()); }
    }

    /// <summary>C# LINQ <c>into</c> clause.</summary>
    public sealed class CsLinqIntoClause : CsLinqElement
    {
        /// <summary>The variable name to assign the previous result to.</summary>
        public string ItemName;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString() { return string.Concat("into ", ItemName.Sanitize()); }
    }
    #endregion

    #region Miscellaneous
    /// <summary>
    ///     C# name/expression pair. Used for property setters in <see cref="CsCustomAttribute"/> and for variable names and
    ///     initialization expressions in <see cref="CsEvent"/>, <see cref="CsField"/> and <see
    ///     cref="CsVariableDeclarationStatement"/>.</summary>
    public sealed class CsNameAndExpression : CsNode
    {
        /// <summary>The name of the variable or property.</summary>
        public string Name;
        /// <summary>The initialization expression for the variable or property.</summary>
        public CsExpression Expression;
        /// <summary>Converts this C# parse tree node back into equivalent C# code.</summary>
        public override string ToString()
        {
            if (Expression == null)
                return Name.Sanitize();
            return string.Concat(Name.Sanitize(), " = ", Expression.ToString());
        }
    }

    /// <summary>Specifies the way in which an argument can be passed to a method.</summary>
    public enum ArgumentMode
    {
        /// <summary>The argument is passed normally (without <c>out</c> or <c>ref</c>).</summary>
        In,
        /// <summary>The argument is an <c>out</c> argument.</summary>
        Out,
        /// <summary>The argument is a <c>ref</c> argument.</summary>
        Ref
    }

    /// <summary>
    ///     C# argument, used in <see cref="CsCustomAttribute"/>, <see cref="CsFunctionCallExpression"/>, <see
    ///     cref="CsNewConstructorExpression"/> and <see cref="CsConstructor"/> (for the base constructor call).</summary>
    public sealed class CsArgument : CsNode
    {
        /// <summary>Name of the argument, or <c>null</c> if it is a position argument.</summary>
        public string Name;
        /// <summary>Specifies whether the argument is <c>out</c>, <c>ref</c> or neither.</summary>
        public ArgumentMode Mode;
        /// <summary>The expression to pass as the actual argument.</summary>
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
