using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;
using System.Text.RegularExpressions;
using System.Diagnostics;
using RT.Util.Xml;

namespace ParseCs
{
    public static class Extensions
    {
        public static string Indent(this string input)
        {
            return Regex.Replace(input, "^(?!$)", "    ", RegexOptions.Multiline);
        }
        public static string CsEscape(this char ch, bool singleQuote, bool doubleQuote)
        {
            switch (ch)
            {
                case '\\': return "\\\\";
                case '\0': return "\\0";
                case '\a': return "\\a";
                case '\b': return "\\b";
                case '\f': return "\\f";
                case '\n': return "\\n";
                case '\r': return "\\r";
                case '\t': return "\\t";
                case '\v': return "\\v";
                case '\'': return singleQuote ? "\\'" : "'";
                case '"': return doubleQuote ? "\\\"" : "\"";
                default: return ch.ToString();
            }
        }
    }

    [XmlIgnoreIfDefault, XmlIgnoreIfEmpty]
    public abstract class CsNode { }

    public class CsDocument : CsNode
    {
        public List<CsUsingNamespace> UsingNamespaces = new List<CsUsingNamespace>();
        public List<CsUsingAlias> UsingAliases = new List<CsUsingAlias>();
        public List<CsCustomAttributeGroup> CustomAttributes = new List<CsCustomAttributeGroup>();
        public List<CsNamespace> Namespaces = new List<CsNamespace>();
        public List<CsType> Types = new List<CsType>();

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var ns in UsingNamespaces)
                sb.Append(ns.ToString());
            if (UsingNamespaces.Any())
                sb.Append('\n');

            foreach (var ns in UsingAliases)
                sb.Append(ns.ToString());
            if (UsingAliases.Any())
                sb.Append('\n');

            foreach (var attr in CustomAttributes)
                sb.Append(attr.ToString());
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

    public abstract class CsUsing : CsNode { }
    public class CsUsingNamespace : CsUsing
    {
        public string Namespace;
        public override string ToString() { return "using " + Namespace + ";\n"; }
    }
    public class CsUsingAlias : CsUsing
    {
        public string Alias;
        public CsTypeIdentifier Original;
        public override string ToString() { return "using " + Alias + " = " + Original + ";\n"; }
    }

    public class CsNamespace : CsNode
    {
        public string Name;
        public List<CsUsingNamespace> UsingNamespaces = new List<CsUsingNamespace>();
        public List<CsUsingAlias> UsingAliases = new List<CsUsingAlias>();
        public List<CsNamespace> Namespaces = new List<CsNamespace>();
        public List<CsType> Types = new List<CsType>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("namespace ");
            sb.Append(Name);
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

    public abstract class CsMember : CsNode
    {
        public List<CsCustomAttributeGroup> CustomAttributes;
        public bool IsInternal, IsPrivate, IsProtected, IsPublic, IsNew;
        protected virtual StringBuilder modifiersCs()
        {
            var sb = new StringBuilder(CustomAttributes.Select(c => c.ToString()).JoinString());
            if (IsProtected) sb.Append("protected ");
            if (IsInternal) sb.Append("internal ");
            if (IsPrivate) sb.Append("private ");
            if (IsPublic) sb.Append("public ");
            if (IsNew) sb.Append("new ");
            return sb;
        }
    }

    public abstract class CsMultiMember : CsMember
    {
        public CsTypeIdentifier Type;
        public List<Tuple<string, CsExpression>> NamesAndInitializers = new List<Tuple<string, CsExpression>>();

        public bool IsAbstract, IsVirtual, IsOverride, IsSealed, IsStatic;

        protected override StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            if (IsAbstract) sb.Append("abstract ");
            if (IsVirtual) sb.Append("virtual ");
            if (IsOverride) sb.Append("override ");
            if (IsSealed) sb.Append("sealed ");
            if (IsStatic) sb.Append("static ");
            return sb;
        }
    }

    public abstract class CsType : CsMember
    {
        public string Name;
    }
    public abstract class CsTypeCanBeGeneric : CsType
    {
        public List<Tuple<string, List<CsCustomAttributeGroup>>> GenericTypeParameters = null;
        public Dictionary<string, List<CsGenericTypeConstraint>> GenericTypeConstraints = null;

        protected string genericTypeParametersCs()
        {
            if (GenericTypeParameters == null)
                return string.Empty;
            return string.Concat("<", GenericTypeParameters.Select(g => g.E2.Select(ca => ca.ToString()).JoinString() + g.E1).JoinString(", "), ">");
        }

        protected string genericTypeConstraintsCs()
        {
            if (GenericTypeConstraints == null)
                return string.Empty;
            return GenericTypeConstraints.Select(kvp => " where " + kvp.Key + " : " + kvp.Value.Select(c => c.ToString()).JoinString(", ")).JoinString();
        }
    }
    public abstract class CsTypeLevel2 : CsTypeCanBeGeneric
    {
        public bool IsPartial;

        public List<CsTypeIdentifier> BaseTypes = null;
        public List<CsMember> Members = new List<CsMember>();

        protected abstract string typeTypeCs { get; }
        protected override StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            if (IsPartial) sb.Append("partial ");
            return sb;
        }
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(typeTypeCs);
            sb.Append(' ');
            sb.Append(Name);
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
    }
    public class CsInterface : CsTypeLevel2
    {
        protected override string typeTypeCs { get { return "interface"; } }
    }
    public class CsStruct : CsTypeLevel2
    {
        protected override string typeTypeCs { get { return "struct"; } }
    }
    public class CsClass : CsTypeLevel2
    {
        public bool IsAbstract, IsSealed, IsStatic;

        protected override string typeTypeCs { get { return "class"; } }
        protected override StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            if (IsAbstract) sb.Append("abstract ");
            if (IsSealed) sb.Append("sealed ");
            if (IsStatic) sb.Append("static ");
            return sb;
        }
    }
    public class CsDelegate : CsTypeCanBeGeneric
    {
        public CsTypeIdentifier ReturnType;
        public List<CsParameter> Parameters = new List<CsParameter>();
        public bool IsUnsafe;
        public override string ToString()
        {
            var sb = modifiersCs();
            if (IsUnsafe) sb.Append("unsafe ");
            sb.Append("delegate ");
            sb.Append(ReturnType.ToString());
            sb.Append(' ');
            sb.Append(Name);
            sb.Append(genericTypeParametersCs());
            sb.Append('(');
            sb.Append(Parameters.Select(p => p.ToString()).JoinString(", "));
            sb.Append(')');
            sb.Append(genericTypeConstraintsCs());
            sb.Append(";\n");
            return sb.ToString();
        }
    }
    public class CsEnum : CsType
    {
        public CsTypeIdentifier BaseType;
        public List<CsEnumValue> EnumValues = new List<CsEnumValue>();
        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append("enum ");
            sb.Append(Name);
            if (!EnumValues.Any())
            {
                sb.Append(" { }\n");
                return sb.ToString();
            }
            sb.Append("\n{\n");
            sb.Append(EnumValues.Select(ev => ev.ToString()).JoinString().Indent());
            sb.Remove(sb.Length - 2, 1);  // remove the last comma from the last enum value
            sb.Append("}\n");
            return sb.ToString();
        }
    }
    public class CsEnumValue : CsNode
    {
        public List<CsCustomAttributeGroup> CustomAttributes;
        public string Name;
        public CsExpression LiteralValue;
        public override string ToString()
        {
            var sb = new StringBuilder(CustomAttributes.Select(c => c.ToString()).JoinString());
            if (LiteralValue == null)
            {
                sb.Append(Name);
                sb.Append(",\n");
            }
            else
            {
                sb.Append(Name);
                sb.Append(" = ");
                sb.Append(LiteralValue.ToString());
                sb.Append(",\n");
            }
            return sb.ToString();
        }
    }
    public abstract class CsMemberLevel2 : CsMember
    {
        public string Name;
        public CsTypeIdentifier ImplementsFrom;
        public bool IsAbstract, IsVirtual, IsOverride, IsSealed, IsStatic, IsExtern, IsUnsafe;

        public CsTypeIdentifier Type;  // for methods, this is the return type

        protected override StringBuilder modifiersCs()
        {
            var sb = base.modifiersCs();
            if (IsAbstract) sb.Append("abstract ");
            if (IsVirtual) sb.Append("virtual ");
            if (IsOverride) sb.Append("override ");
            if (IsSealed) sb.Append("sealed ");
            if (IsStatic) sb.Append("static ");
            if (IsExtern) sb.Append("extern ");
            if (IsUnsafe) sb.Append("unsafe ");
            return sb;
        }
    }
    public class CsEvent : CsMultiMember
    {
#warning TODO: 'Add' and 'remove' handlers

        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append("event ");
            sb.Append(Type.ToString());
            sb.Append(' ');
            for (int i = 0; i < NamesAndInitializers.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                var tuple = NamesAndInitializers[i];
                sb.Append(tuple.E1);
                if (tuple.E2 != null)
                {
                    sb.Append(" = ");
                    sb.Append(tuple.E2.ToString());
                }
            }
#warning TODO: 'Add' and 'remove' handlers
            // if(add_and_remove_handlers) {
            // sb.Append("{\n" ........);
            // } else
            sb.Append(";\n");
            return sb.ToString();
        }
    }
    public class CsField : CsMultiMember
    {
        public bool IsReadonly;
        public bool IsConst;
        public override string ToString()
        {
            var sb = modifiersCs();
            if (IsReadonly) sb.Append("readonly ");
            if (IsConst) sb.Append("const ");
            sb.Append(Type.ToString());
            sb.Append(' ');
            for (int i = 0; i < NamesAndInitializers.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                var tuple = NamesAndInitializers[i];
                sb.Append(tuple.E1);
                if (tuple.E2 != null)
                {
                    sb.Append(" = ");
                    sb.Append(tuple.E2.ToString());
                }
            }
            sb.Append(";\n");
            return sb.ToString();
        }
    }
    public class CsProperty : CsMemberLevel2
    {
        public List<CsSimpleMethod> Methods = new List<CsSimpleMethod>();
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
            sb.Append(Name);
            sb.Append("\n{\n");
            sb.Append(Methods.Select(m => m.ToString()).JoinString().Indent());
            sb.Append("}\n");
            return sb.ToString();
        }
    }
    public class CsIndexedProperty : CsProperty
    {
        public List<CsParameter> Parameters;
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
    public enum MethodType { Get, Set, Add, Remove };
    public class CsSimpleMethod : CsMember
    {
        public MethodType Type;
        public CsBlock Body;

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
    public class CsMethod : CsMemberLevel2
    {
        public List<CsParameter> Parameters = new List<CsParameter>();
        public List<Tuple<string, List<CsCustomAttributeGroup>>> GenericTypeParameters = null;
        public Dictionary<string, List<CsGenericTypeConstraint>> GenericTypeConstraints = null;
        public CsBlock MethodBody;

        protected string genericTypeParametersCs()
        {
            if (GenericTypeParameters == null)
                return string.Empty;
            return string.Concat("<", GenericTypeParameters.Select(g => g.E2.Select(ca => ca.ToString()).JoinString() + g.E1).JoinString(", "), ">");
        }

        protected string genericTypeConstraintsCs()
        {
            if (GenericTypeConstraints == null)
                return string.Empty;
            return GenericTypeConstraints.Select(kvp => " where " + kvp.Key + " : " + kvp.Value.Select(c => c.ToString()).JoinString(", ")).JoinString();
        }

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
            sb.Append(Name);
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
    }
    public enum ConstructorCallType { None, This, Base };
    public class CsConstructor : CsMember
    {
        public string Name;
        public List<CsParameter> Parameters = new List<CsParameter>();
        public CsBlock MethodBody;
        public ConstructorCallType CallType;
        public List<CsExpression> CallParameters;

        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append(Name);
            sb.Append('(');
            sb.Append(Parameters.Select(p => p.ToString()).JoinString(", "));
            sb.Append(')');

            if (CallType != ConstructorCallType.None)
            {
                sb.Append(CallType == ConstructorCallType.Base ? " : base(" : CallType == ConstructorCallType.This ? " : this(" : null);
                sb.Append(CallParameters.Select(p => p.ToString()).JoinString(", "));
                sb.Append(')');
            }

            sb.Append('\n');
            sb.Append(MethodBody.ToString());
            return sb.ToString();
        }
    }
    public class CsDestructor : CsMember
    {
        public string Name;
        public CsBlock MethodBody;

        public override string ToString()
        {
            var sb = modifiersCs();
            sb.Append('~');
            sb.Append(Name);
            sb.Append("()\n");
            sb.Append(MethodBody.ToString());
            return sb.ToString();
        }
    }

    public class CsParameter : CsNode
    {
        public List<CsCustomAttributeGroup> CustomAttributes;
        public CsTypeIdentifier Type;
        public string Name;
        public bool IsThis;
        public bool IsOut;
        public bool IsRef;
        public bool IsParams;
        public override string ToString()
        {
            return string.Concat(CustomAttributes.Select(c => c.ToString()).JoinString(), IsThis ? "this " : string.Empty, IsParams ? "params " : string.Empty, IsOut ? "out " : string.Empty, IsRef ? "ref " : string.Empty, Type.ToString(), " ", Name);
        }
    }

    public abstract class CsTypeIdentifier : CsNode
    {
        public abstract bool IsSingleIdentifier();
    }

    public class CsEmptyGenericTypeIdentifier : CsTypeIdentifier
    {
        public override string ToString() { return string.Empty; }
        public override bool IsSingleIdentifier() { return false; }
    }
    public class CsConcreteTypeIdentifierPart : CsNode
    {
        public string Name;
        public List<CsTypeIdentifier> GenericTypeParameters = null;
        public override string ToString() { return GenericTypeParameters == null ? Name : string.Concat(Name, '<', GenericTypeParameters.Select(p => p.ToString()).JoinString(", "), '>'); }
    }
    public class CsConcreteTypeIdentifier : CsTypeIdentifier
    {
        public bool HasGlobal;
        public List<CsConcreteTypeIdentifierPart> Parts = new List<CsConcreteTypeIdentifierPart>();
        public override string ToString() { return (HasGlobal ? "global::" : string.Empty) + Parts.Select(p => p.ToString()).JoinString("."); }
        public override bool IsSingleIdentifier() { return !HasGlobal && Parts.Count == 1 && Parts[0].GenericTypeParameters == null; }
    }
    public class CsArrayTypeIdentifier : CsTypeIdentifier
    {
        public CsTypeIdentifier InnerType;
        public List<int> ArrayRanks;
        public override string ToString() { return InnerType.ToString() + ArrayRanks.Select(rank => string.Concat("[", new string(',', rank - 1), "]")).JoinString(); }
        public override bool IsSingleIdentifier() { return false; }
    }
    public class CsPointerTypeIdentifier : CsTypeIdentifier
    {
        public CsTypeIdentifier InnerType;
        public override string ToString() { return InnerType.ToString() + "*"; }
        public override bool IsSingleIdentifier() { return false; }
    }
    public class CsNullableTypeIdentifier : CsTypeIdentifier
    {
        public CsTypeIdentifier InnerType;
        public override string ToString() { return InnerType.ToString() + "?"; }
        public override bool IsSingleIdentifier() { return false; }
    }

    public abstract class CsGenericTypeConstraint : CsNode { }
    public class CsGenericTypeConstraintNew : CsGenericTypeConstraint { public override string ToString() { return "new()"; } }
    public class CsGenericTypeConstraintClass : CsGenericTypeConstraint { public override string ToString() { return "class"; } }
    public class CsGenericTypeConstraintStruct : CsGenericTypeConstraint { public override string ToString() { return "struct"; } }
    public class CsGenericTypeConstraintBaseClass : CsGenericTypeConstraint
    {
        public CsTypeIdentifier BaseClass;
        public override string ToString() { return BaseClass.ToString(); }
    }

    public class CsCustomAttribute : CsNode
    {
        public CsTypeIdentifier Type;
        public List<CsExpression> Positional = new List<CsExpression>();
        public List<Tuple<string, CsExpression>> Named = new List<Tuple<string, CsExpression>>();
        public override string ToString()
        {
            if (Positional.Count + Named.Count == 0)
                return Type.ToString();
            return string.Concat(Type.ToString(), '(', Positional.Select(p => p.ToString()).Concat(Named.Select(p => p.ToString())).JoinString(", "), ')');
        }
    }
    public enum CustomAttributeLocation { None, Assembly, Module, Type, Method, Property, Field, Event, Param, Return, Typevar }
    public class CsCustomAttributeGroup : CsNode
    {
        public CustomAttributeLocation Location;
        public List<CsCustomAttribute> CustomAttributes;
        public bool NoNewLine = false;
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
            sb.Append(NoNewLine ? "] " : "]\n");
            return sb.ToString();
        }
    }

    public abstract class CsStatement : CsNode
    {
        public List<string> GotoLabels;
        protected string gotoLabels() { return GotoLabels == null ? string.Empty : GotoLabels.Select(g => g + ':').JoinString(" ") + (this is CsEmptyStatement ? " " : "\n"); }
    }
    public class CsEmptyStatement : CsStatement { public override string ToString() { return gotoLabels() + ";\n"; } }
    public class CsBlock : CsStatement
    {
        public List<CsStatement> Statements = new List<CsStatement>();
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
        public override string ToString()
        {
            if (Expression == null)
                return string.Concat(gotoLabels(), Keyword, ";\n");
            return string.Concat(gotoLabels(), Keyword, ' ', Expression.ToString(), ";\n");
        }
    }
    public class CsReturnStatement : CsOptionalExpressionStatement { public override string Keyword { get { return "return"; } } }
    public class CsThrowStatement : CsOptionalExpressionStatement { public override string Keyword { get { return "throw"; } } }
    public abstract class CsBlockStatement : CsStatement { public CsBlock Block; }
    public class CsCheckedStatement : CsBlockStatement { public override string ToString() { return string.Concat(gotoLabels(), "checked\n", Block.ToString()); } }
    public class CsUncheckedStatement : CsBlockStatement { public override string ToString() { return string.Concat(gotoLabels(), "unchecked\n", Block.ToString()); } }
    public class CsUnsafeStatement : CsBlockStatement { public override string ToString() { return string.Concat(gotoLabels(), "unsafe\n", Block.ToString()); } }
    public class CsSwitchStatement : CsStatement
    {
        public CsExpression SwitchOn;
        public List<CsCaseLabel> Cases = new List<CsCaseLabel>();
        public override string ToString() { return string.Concat(gotoLabels(), "switch (", SwitchOn.ToString(), ")\n{\n", Cases.Select(c => c.ToString().Indent()).JoinString("\n"), "}\n"); }
    }
    public class CsCaseLabel : CsNode
    {
        public List<CsExpression> CaseValues = new List<CsExpression>();  // use a 'null' expression for the 'default' label
        public List<CsStatement> Statements;
        public override string ToString() { return string.Concat(CaseValues.Select(c => c == null ? "default:\n" : "case " + c.ToString() + ":\n").JoinString(), Statements.Select(s => s.ToString().Indent()).JoinString()); }
    }
    public class CsExpressionStatement : CsStatement { public CsExpression Expression; public override string ToString() { return string.Concat(gotoLabels(), Expression.ToString(), ";\n"); } }
    public class CsVariableDeclarationStatement : CsStatement
    {
        public CsTypeIdentifier Type;
        public List<Tuple<string, CsExpression>> NamesAndInitializers = new List<Tuple<string, CsExpression>>();
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append(Type.ToString());
            sb.Append(' ');
            sb.Append(NamesAndInitializers.Select(n => n.E1 + (n.E2 == null ? string.Empty : " = " + n.E2.ToString())).JoinString(", "));
            sb.Append(";\n");
            return sb.ToString();
        }
    }
    public class CsForeachStatement : CsStatement
    {
        public CsTypeIdentifier VariableType;
        public string VariableName;
        public CsExpression LoopExpression;
        public CsStatement Body;
        public override string ToString() { return string.Concat(gotoLabels(), "foreach (", VariableType == null ? string.Empty : VariableType.ToString() + ' ', VariableName, " in ", LoopExpression.ToString(), ")\n", Body is CsBlock ? Body.ToString() : Body.ToString().Indent()); }
    }
    public class CsForStatement : CsStatement
    {
        public CsStatement InitializationStatement;
        public CsExpression TerminationCondition;
        public CsExpression LoopExpression;
        public CsStatement Body;
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("for (");
            if (InitializationStatement != null)
                sb.Append(InitializationStatement.ToString().Trim());
            else
                sb.Append(';');
            if (TerminationCondition != null)
            {
                sb.Append(' ');
                sb.Append(TerminationCondition.ToString());
            }
            sb.Append(';');
            if (LoopExpression != null)
            {
                sb.Append(' ');
                sb.Append(LoopExpression.ToString());
            }
            sb.Append(")\n");
            sb.Append(Body is CsBlock ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }
    public class CsUsingStatement : CsStatement
    {
        public CsStatement InitializationStatement;
        public CsStatement Body;
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("using (");
            sb.Append(InitializationStatement.ToString());
            sb.Append(")\n");
            sb.Append(Body is CsBlock || Body is CsUsingStatement ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }
    public class CsFixedStatement : CsStatement
    {
        public CsStatement InitializationStatement;
        public CsStatement Body;
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("fixed (");
            sb.Append(InitializationStatement.ToString());
            sb.Append(")\n");
            sb.Append(Body is CsBlock || Body is CsFixedStatement ? Body.ToString() : Body.ToString().Indent());
            return sb.ToString();
        }
    }
    public class CsIfStatement : CsStatement
    {
        public CsExpression IfExpression;
        public CsStatement Statement;
        public CsStatement ElseStatement;
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
        public CsStatement Statement;
        public abstract string Keyword { get; }
        public override string ToString() { return string.Concat(gotoLabels(), Keyword, " (", Expression.ToString(), ")\n", Statement is CsBlock ? Statement.ToString() : Statement.ToString().Indent()); }
    }
    public class CsWhileStatement : CsExpressionBlockStatement { public override string Keyword { get { return "while"; } } }
    public class CsLockStatement : CsExpressionBlockStatement { public override string Keyword { get { return "lock"; } } }
    public class CsDoWhileStatement : CsStatement
    {
        public CsExpression WhileExpression;
        public CsStatement Statement;
        public override string ToString() { return string.Concat(gotoLabels(), "do\n", Statement is CsBlock ? Statement.ToString() : Statement.ToString().Indent(), "while (", WhileExpression.ToString(), ");\n"); }
    }
    public class CsTryStatement : CsStatement
    {
        public CsBlock Block;
        public List<CsCatchClause> Catches = new List<CsCatchClause>();
        public CsBlock Finally;
        public override string ToString()
        {
            var sb = new StringBuilder(gotoLabels());
            sb.Append("try\n");
            sb.Append(Block.ToString());
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
    public class CsCatchClause : CsNode
    {
        public CsTypeIdentifier Type;
        public string Name;
        public CsBlock Block;
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
                    sb.Append(Name);
                }
                sb.Append(")");
            }
            sb.Append("\n");
            sb.Append(Block is CsBlock ? Block.ToString() : Block.ToString().Indent());
            return sb.ToString();
        }
    }
    public class CsGotoStatement : CsStatement { public string Label; public override string ToString() { return string.Concat(gotoLabels(), "goto ", Label, ";\n"); } }
    public class CsContinueStatement : CsStatement { public override string ToString() { return string.Concat(gotoLabels(), "continue;\n"); } }
    public class CsBreakStatement : CsStatement { public override string ToString() { return string.Concat(gotoLabels(), "break;\n"); } }
    public class CsYieldBreakStatement : CsStatement { public override string ToString() { return string.Concat(gotoLabels(), "yield break;\n"); } }
    public class CsYieldReturnStatement : CsStatement { public CsExpression Expression; public override string ToString() { return string.Concat(gotoLabels(), "yield return ", Expression.ToString(), ";\n"); } }

    public abstract class CsExpression : CsNode { }
    public enum AssignmentOperator { Eq, TimesEq, DivEq, ModEq, PlusEq, MinusEq, ShlEq, ShrEq, AndEq, XorEq, OrEq }
    public class CsAssignmentExpression : CsExpression
    {
        public AssignmentOperator Operator;
        public CsExpression Left, Right;
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
    }
    public class CsConditionalExpression : CsExpression
    {
        public CsExpression Left, Middle, Right;
        public override string ToString()
        {
            return string.Concat(Left.ToString(), " ? ", Middle.ToString(), " : ", Right.ToString());
        }
    }
    public enum BinaryOperator { Times, Div, Mod, Plus, Minus, Shl, Shr, Less, Greater, LessEq, GreaterEq, Eq, NotEq, And, Xor, Or, AndAnd, OrOr, Coalesce }
    public class CsBinaryOperatorExpression : CsExpression
    {
        public BinaryOperator Operator;
        public CsExpression Left, Right;
        public override string ToString()
        {
            return string.Concat(
                Left.ToString(),
                Operator == BinaryOperator.Times ? " * " :
                Operator == BinaryOperator.Div ? " / " :
                Operator == BinaryOperator.Mod ? " % " :
                Operator == BinaryOperator.Plus ? " + " :
                Operator == BinaryOperator.Minus ? " - " :
                Operator == BinaryOperator.Shl ? " << " :
                Operator == BinaryOperator.Shr ? " >> " :
                Operator == BinaryOperator.Less ? " < " :
                Operator == BinaryOperator.Greater ? " > " :
                Operator == BinaryOperator.LessEq ? " <= " :
                Operator == BinaryOperator.GreaterEq ? " >= " :
                Operator == BinaryOperator.Eq ? " == " :
                Operator == BinaryOperator.NotEq ? " != " :
                Operator == BinaryOperator.And ? " & " :
                Operator == BinaryOperator.Xor ? " ^ " :
                Operator == BinaryOperator.Or ? " | " :
                Operator == BinaryOperator.AndAnd ? " && " :
                Operator == BinaryOperator.OrOr ? " || " : null,
                Right.ToString()
            );
        }
    }
    public enum BinaryTypeOperator { Is, As }
    public class CsBinaryTypeOperatorExpression : CsExpression
    {
        public BinaryTypeOperator Operator;
        public CsExpression Left;
        public CsTypeIdentifier Right;
        public override string ToString()
        {
            return string.Concat(
                Left.ToString(),
                Operator == BinaryTypeOperator.Is ? " is " :
                Operator == BinaryTypeOperator.As ? " as " : null,
                Right.ToString()
            );
        }
    }
    public enum UnaryOperator { Plus, Minus, Not, Neg, PrefixInc, PrefixDec, PostfixInc, PostfixDec, PointerDeref, AddressOf }
    public class CsUnaryOperatorExpression : CsExpression
    {
        public UnaryOperator Operator;
        public CsExpression Operand;
        public override string ToString()
        {
            if (Operator == UnaryOperator.PostfixInc)
                return Operand.ToString() + "++";
            if (Operator == UnaryOperator.PostfixDec)
                return Operand.ToString() + "--";
            return (
                Operator == UnaryOperator.Plus ? "+" :
                Operator == UnaryOperator.Minus ? "-" :
                Operator == UnaryOperator.Not ? "!" :
                Operator == UnaryOperator.Neg ? "~" :
                Operator == UnaryOperator.PrefixInc ? "++" :
                Operator == UnaryOperator.PrefixDec ? "--" :
                Operator == UnaryOperator.PointerDeref ? "*" :
                Operator == UnaryOperator.AddressOf ? "&" : null
            ) + Operand.ToString();
        }
    }
    public class CsCastExpression : CsExpression
    {
        public CsTypeIdentifier Type;
        public CsExpression Operand;
        public override string ToString() { return string.Concat('(', Type.ToString(), ") ", Operand.ToString()); }
    }
    public class CsMemberAccessExpression : CsExpression
    {
        public CsExpression Left, Right;
        public override string ToString() { return string.Concat(Left.ToString(), ".", Right.ToString()); }
    }
    public enum ParameterType { In, Out, Ref };
    public class CsFunctionCallExpression : CsExpression
    {
        public bool IsIndexer;
        public CsExpression Left;
        public List<Tuple<ParameterType, CsExpression>> Parameters = new List<Tuple<ParameterType, CsExpression>>();
        public override string ToString() { return string.Concat(Left.ToString(), IsIndexer ? '[' : '(', Parameters.Select(p => (p.E1 == ParameterType.Out ? "out " : p.E1 == ParameterType.Ref ? "ref " : string.Empty) + p.E2.ToString()).JoinString(", "), IsIndexer ? ']' : ')'); }
    }
    public abstract class CsTypeOperatorExpression : CsExpression { public CsTypeIdentifier Type; }
    public class CsTypeofExpression : CsTypeOperatorExpression { public override string ToString() { return string.Concat("typeof(", Type.ToString(), ')'); } }
    public class CsDefaultExpression : CsTypeOperatorExpression { public override string ToString() { return string.Concat("default(", Type.ToString(), ')'); } }
    public abstract class CsCheckedUncheckedExpression : CsExpression { public CsExpression Subexpression;    }
    public class CsCheckedExpression : CsCheckedUncheckedExpression { public override string ToString() { return string.Concat("checked(", Subexpression.ToString(), ')'); } }
    public class CsUncheckedExpression : CsCheckedUncheckedExpression { public override string ToString() { return string.Concat("unchecked(", Subexpression.ToString(), ')'); } }
    public class CsTypeIdentifierExpression : CsExpression
    {
        public CsTypeIdentifier Type;
        public override string ToString() { return Type.ToString(); }
    }
    public class CsIdentifierExpression : CsExpression
    {
        public string Identifier;
        public override string ToString() { return Identifier; }
    }
    public class CsParenthesizedExpression : CsExpression { public CsExpression Subexpression; public override string ToString() { return string.Concat('(', Subexpression.ToString(), ')'); } }
    public class CsStringLiteralExpression : CsExpression
    {
        private static char[] SpecialCharacters1 = new[] { '\0', '\a', '\b', '\f', '\r', '\t', '\v' };
        private static char[] SpecialCharacters2 = new[] { '"', '\\' };
        public string Literal;
        public override string ToString()
        {
            bool useVerbatim;

            // If the string contains any of the crazy-but-escapable characters, use those escape sequences.
            if (Literal.Any(ch => SpecialCharacters1.Contains(ch)))
                useVerbatim = false;
            // Otherwise, if the string contains a double-quote or backslash, use verbatim.
            else if (Literal.Any(ch => SpecialCharacters2.Contains(ch)))
                useVerbatim = true;
            // Otherwise, if it's 3 lines or more, not counting leading or trailing newlines, use verbatim.
            else if (Literal.Trim().Count(ch => ch == '\n') >= 3)
                useVerbatim = true;
            // In all other cases, esp. those where you just have a "\n" at the end, use escape sequences.
            else
                useVerbatim = false;

            if (useVerbatim)
                return string.Concat('@', '"', Literal.Split('"').JoinString("\"\""), '"');
            else
                return string.Concat('"', Literal.Select(ch => ch.CsEscape(false, true)).JoinString(), '"');
        }
    }
    public class CsCharacterLiteralExpression : CsExpression
    {
        public char Literal;
        public override string ToString() { return string.Concat('\'', Literal.CsEscape(true, false), '\''); }
    }
    public class CsNumberLiteralExpression : CsExpression
    {
        public string Literal;  // Could break this down further, but this is the safest
        public override string ToString() { return Literal; }
    }
    public class CsBooleanLiteralExpression : CsExpression { public bool Literal; public override string ToString() { return Literal ? "true" : "false"; } }
    public class CsNullExpression : CsExpression { public override string ToString() { return "null"; } }
    public class CsThisExpression : CsExpression { public override string ToString() { return "this"; } }
    public class CsBaseExpression : CsExpression { public override string ToString() { return "base"; } }
    public class CsInitializer : CsNode
    {
        public string Name;
        public CsExpression Expression;
        public override string ToString() { return string.Concat(Name, " = ", Expression); }
    }
    public class CsNewConstructorExpression : CsExpression
    {
        public CsTypeIdentifier Type;
        public List<Tuple<ParameterType, CsExpression>> Parameters = new List<Tuple<ParameterType, CsExpression>>();
        public List<CsInitializer> Initializers;
        public List<CsExpression> Adds;
        public override string ToString()
        {
            var sb = new StringBuilder("new ");
            sb.Append(Type.ToString());
            if (Parameters.Any() || (Initializers == null && Adds == null))
            {
                sb.Append('(');
                sb.Append(Parameters.Select(p => (p.E1 == ParameterType.Out ? "out " : p.E1 == ParameterType.Ref ? "ref " : string.Empty) + p.E2.ToString()).JoinString(", "));
                sb.Append(')');
            }
            if (Initializers != null)
            {
                sb.Append(" { ");
                sb.Append(Initializers.Select(ini => ini.ToString()).JoinString(", "));
                sb.Append(" }");
            }
            else if (Adds != null && Adds.Count == 0)
                sb.Append(" { }");
            else if (Adds != null)
            {
                sb.Append(" { ");
                sb.Append(Adds.Select(add => add.ToString()).JoinString(", "));
                sb.Append(" }");
            }
            return sb.ToString();
        }
    }
    public class CsNewAnonymousTypeExpression : CsExpression
    {
        public List<CsInitializer> Initializers = new List<CsInitializer>();
        public override string ToString() { return string.Concat("new { ", Initializers.Select(ini => ini.ToString()).JoinString(", "), " }"); }
    }
    public class CsNewImplicitlyTypedArrayExpression : CsExpression
    {
        public List<CsExpression> Items = new List<CsExpression>();
        public override string ToString() { return Items.Count == 0 ? "new[] { }" : string.Concat("new[] { ", Items.Select(p => p.ToString()).JoinString(", "), " }"); }
    }
    public class CsNewArrayExpression : CsExpression
    {
        public CsTypeIdentifier Type;
        public List<CsExpression> SizeExpressions = new List<CsExpression>();
        public List<int> AdditionalRanks = new List<int>();
        public List<CsExpression> Items = new List<CsExpression>();
        public override string ToString()
        {
            var sb = new StringBuilder("new ");
            sb.Append(Type.ToString());
            sb.Append('[');
            sb.Append(SizeExpressions.Select(s => s.ToString()).JoinString(", "));
            sb.Append(']');
            sb.Append(AdditionalRanks.Select(a => "[" + new string(',', a - 1) + ']'));
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
    }
    public abstract class CsLambaExpression : CsExpression
    {
        public List<string> ParameterNames = new List<string>();
    }
    public class CsSimpleLambdaExpression : CsLambaExpression
    {
        public CsExpression Expression;
        public override string ToString() { return string.Concat(ParameterNames.Count == 1 ? ParameterNames[0] : string.Concat('(', ParameterNames.JoinString(", "), ')'), " => ", Expression.ToString()); }
    }
    public class CsBlockLambdaExpression : CsLambaExpression
    {
        public CsBlock Block;
        public override string ToString() { return string.Concat(ParameterNames.Count == 1 ? ParameterNames[0] : string.Concat('(', ParameterNames.JoinString(", "), ')'), " =>\n", Block.ToString(), '\n'); }
    }
    public class CsArrayLiteralExpression : CsExpression
    {
        public List<CsExpression> Expressions = new List<CsExpression>();
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
    }
}
