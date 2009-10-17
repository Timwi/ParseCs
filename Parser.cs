using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RT.Util.ExtensionMethods;
using RT.Util.Collections;
using RT.Util;
using System.Diagnostics;

namespace ParseCs
{
    public static class Parser
    {
        /// <summary>
        /// Parses the specified C# source code into a document tree.
        /// </summary>
        /// <param name="source">C# source code to parse.</param>
        /// <exception cref="ParseException">The specified C# source code could not be parsed.</exception>
        public static CsDocument Parse(string source)
        {
            var tokens = Lexer.Lex(source, Lexer.LexOptions.IgnoreComments);
            int tokenIndex = 0;
            return parseDocument(tokens, ref tokenIndex);
        }

        private static CsDocument parseDocument(TokenJar tok, ref int i)
        {
            var doc = new CsDocument();
            try
            {
                while (tok.IndexExists(i))
                {
                    object result = parseMember(tok, ref i, true);
                    if (result is CsUsingAlias)
                        doc.UsingAliases.Add((CsUsingAlias) result);
                    else if (result is CsUsingNamespace)
                        doc.UsingNamespaces.Add((CsUsingNamespace) result);
                    else if (result is CsNamespace)
                        doc.Namespaces.Add((CsNamespace) result);
                    else if (result is CsType)
                        doc.Types.Add((CsType) result);
                    else if (result is CsCustomAttributeGroup)
                        doc.CustomAttributes.Add((CsCustomAttributeGroup) result);
                    else
                        throw new ParseException("Unexpected element. Expected 'using', 'namespace', or a type declaration.", tok[i].Index, doc);
                }
            }
            catch (LexException e)
            {
                throw new ParseException(e.Message, e.Index, doc);
            }
            catch (ParseException e)
            {
                if (e.IncompleteResult is CsUsingAlias)
                    doc.UsingAliases.Add((CsUsingAlias) e.IncompleteResult);
                else if (e.IncompleteResult is CsUsingNamespace)
                    doc.UsingNamespaces.Add((CsUsingNamespace) e.IncompleteResult);
                else if (e.IncompleteResult is CsNamespace)
                    doc.Namespaces.Add((CsNamespace) e.IncompleteResult);
                else if (e.IncompleteResult is CsType)
                    doc.Types.Add((CsType) e.IncompleteResult);
                throw new ParseException(e.Message, e.Index, doc);
            }
            return doc;
        }

        private static object parseMember(TokenJar tok, ref int i, bool returnAssemblyAndModuleCustomAttributes)
        {
            var customAttribs = new List<CsCustomAttributeGroup>();
            while (tok[i].IsBuiltin("["))
            {
                var k = i;
                var attr = parseCustomAttributeGroup(tok, ref i, false);
                if (returnAssemblyAndModuleCustomAttributes && (attr.Location == CustomAttributeLocation.Assembly || attr.Location == CustomAttributeLocation.Module))
                {
                    if (customAttribs.Count > 0)
                        throw new ParseException(@"Assembly or module custom attribute not allowed after other custom attributes.", tok[k].Index);
                    return attr;
                }
                customAttribs.Add(attr);
            }

            if (tok[i].IsBuiltin("using"))
            {
                if (customAttribs.Count > 0)
                    throw new ParseException("'using' directives cannot have custom attributes.", tok[i].Index);
                return parseUsing(tok, ref i);
            }
            if (tok[i].IsBuiltin("namespace"))
            {
                if (customAttribs.Count > 0)
                    throw new ParseException("Namespaces cannot have custom attributes.", tok[i].Index);
                return parseNamespace(tok, ref i);
            }

            var j = i;
            var modifiers = new[] { "abstract", "const", "extern", "internal", "new", "override", "partial", "private", "protected", "public", "readonly", "sealed", "static", "unsafe", "virtual", "volatile" };
            while ((tok[j].Type == TokenType.Builtin || tok[j].Type == TokenType.Identifier) && modifiers.Contains(tok[j].TokenStr))
                j++;
            if (tok[j].IsBuiltin("class") || tok[j].IsBuiltin("struct") || tok[j].IsBuiltin("interface") || tok[j].IsBuiltin("enum") || tok[j].IsBuiltin("delegate"))
            {
                try
                {
#warning change this so that parseType() doesn't parse the modifiers (perhaps put it in here)
                    var ty = parseType(tok, ref i);
                    ty.CustomAttributes = customAttribs;
                    return ty;
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsType)
                    {
                        ((CsType) e.IncompleteResult).CustomAttributes = customAttribs;
                        throw new ParseException(e.Message, e.Index, (CsType) e.IncompleteResult);
                    }
                    throw;
                }
            }
            else if (tok[j].IsBuiltin("event"))
            {
                CsEvent ev = new CsEvent { CustomAttributes = customAttribs };
                parseModifiers(ev, tok, ref i);
                if (i != j)
                    throw new ParseException("The modifier '{0}' is not valid for events.".Fmt(tok[i].TokenStr), tok[i].Index);
                i = j + 1;

                ev.Type = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowArrays | typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes);

                string name = tok[i].Identifier();
                i++;
                if (tok[i].IsBuiltin("{"))
                {
                    throw new ParseException("Add/remove handlers not implemented.", tok[i].Index, ev);
                }
                CsExpression initializer = null;
                bool canHaveCurly = true;
                bool canHaveEquals = true;

                try
                {
                    if (tok[i].IsBuiltin("="))
                    {
                        canHaveCurly = false;
                        canHaveEquals = false;
                        i++;
                        initializer = parseExpression(tok, ref i);
                    }
                    ev.NamesAndInitializers.Add(Ut.Tuple(name, initializer));
                    while (tok[i].IsBuiltin(","))
                    {
                        canHaveCurly = false;
                        canHaveEquals = true;
                        i++;
                        name = tok[i].Identifier();
                        initializer = null;
                        i++;
                        if (tok[i].IsBuiltin("="))
                        {
                            canHaveEquals = false;
                            i++;
                            initializer = parseExpression(tok, ref i);
                        }
                        ev.NamesAndInitializers.Add(Ut.Tuple(name, initializer));
                    }
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        ev.NamesAndInitializers.Add(Ut.Tuple(name, (CsExpression) e.IncompleteResult));
                    throw new ParseException(e.Message, e.Index, ev);
                }
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException(canHaveCurly ? (canHaveEquals ? "'{', '=', ',' or ';' expected." : "'{', ',' or ';' expected.") : (canHaveEquals ? "'=', ',' or ';' expected." : "',' or ';' expected."), tok[i].Index, ev);
                i++;
                return ev;
            }
            else if (tok[j].Type == TokenType.Identifier && tok[j + 1].IsBuiltin("("))
            {
                // Looks like a constructor declaration
                CsConstructor con = new CsConstructor { Name = tok[j].TokenStr, CustomAttributes = customAttribs };
                parseModifiers(con, tok, ref i);
                if (i != j)
                    throw new ParseException("The modifier '{0}' is not valid for constructors.".Fmt(tok[i].TokenStr), tok[i].Index);
                i = j + 1;
                con.Parameters = parseParameterList(tok, ref i);
                bool canHaveColon = true;
                if (tok[i].IsBuiltin(":"))
                {
                    canHaveColon = false;
                    i++;
                    if (tok[i].IsBuiltin("this"))
                        con.CallType = ConstructorCallType.This;
                    else if (tok[i].IsBuiltin("base"))
                        con.CallType = ConstructorCallType.Base;
                    else
                        throw new ParseException("'this' or 'base' expected.", tok[i].Index);
                    i++;
                    if (!tok[i].IsBuiltin("("))
                        throw new ParseException("'(' expected.", tok[i].Index);
                    i++;
                    con.CallParameters = new List<CsExpression>();
                    con.MethodBody = new CsBlock();  // temporary; just so we can throw a valid constructor as an incomplete result
                    try
                    {
                        if (!tok[i].IsBuiltin(")"))
                        {
                            con.CallParameters.Add(parseExpression(tok, ref i));
                            while (tok[i].IsBuiltin(","))
                            {
                                i++;
                                con.CallParameters.Add(parseExpression(tok, ref i));
                            }
                            if (!tok[i].IsBuiltin(")"))
                                throw new ParseException("',' or ')' expected.", tok[i].Index);
                        }
                        i++;
                    }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsExpression)
                            con.CallParameters.Add((CsExpression) e.IncompleteResult);
                        throw new ParseException(e.Message, e.Index, con);
                    }
                }
                if (!tok[i].IsBuiltin("{"))
                    throw new ParseException(canHaveColon ? "':' or '{' expected." : "'{' expected.", tok[i].Index);
                try { con.MethodBody = parseBlock(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsBlock)
                    {
                        con.MethodBody = (CsBlock) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, con);
                    }
                    throw;
                }
                return con;
            }
            else if (tok[j].IsBuiltin("~") && tok[j + 1].Type == TokenType.Identifier && tok[j + 2].IsBuiltin("("))
            {
                // Looks like a destructor declaration
                CsDestructor des = new CsDestructor { Name = tok[j + 1].TokenStr, CustomAttributes = customAttribs };
                parseModifiers(des, tok, ref i);
                if (i != j)
                    throw new ParseException("The modifier '{0}' is not valid for destructors.".Fmt(tok[i].TokenStr), tok[i].Index);
                i = j + 3;
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException(@"Destructors cannot have any parameters.", tok[i].Index);
                i++;
                if (!tok[i].IsBuiltin("{"))
                    throw new ParseException("'{' expected.", tok[i].Index);
                try { des.MethodBody = parseBlock(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsBlock)
                    {
                        des.MethodBody = (CsBlock) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, des);
                    }
                    throw;
                }
                return des;
            }
            else if (tok[j].IsBuiltin("implicit") || tok[j].IsBuiltin("explicit"))
            {
                var op = new CsCastOperatorOverload { CastType = tok[j].IsBuiltin("implicit") ? CastOperatorType.Implicit : CastOperatorType.Explicit, CustomAttributes = customAttribs };
                parseModifiers(op, tok, ref i);
                if (i != j)
                    throw new ParseException("The modifier '{0}' is not valid for {1} operator declarations.".Fmt(tok[i].TokenStr, tok[j].TokenStr), tok[i].Index);
                i = j + 1;
                if (!tok[i].IsBuiltin("operator"))
                    throw new ParseException("'operator' expected.", tok[i].Index);
                i++;
                op.ReturnType = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowArrays | typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes);
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index);
                var parameters = parseParameterList(tok, ref i);
                if (parameters.Count != 1)
                    throw new ParseException("Implicit/explicit operators must have exactly one parameter.", tok[i].Index, op);
                op.Parameter = parameters[0];
                try { op.MethodBody = parseBlock(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsBlock)
                        op.MethodBody = (CsBlock) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, op);
                }
                return op;
            }
            else if (tok[j].IsBuiltin("operator"))
            {
                throw new ParseException("You must specify either 'implicit operator', 'explicit operator', or a return type before the 'operator' keyword.", tok[j].Index);
            }
            else
            {
                // It could be a field, a method, an operator overload or a property
                var afterModifiers = j;
                var prevIndex = tok[j].Index;
                CsTypeIdentifier type;
                try
                {
                    type = parseTypeIdentifier(tok, ref j, typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays);
                }
                catch (ParseException e)
                {
                    if (e.Index == prevIndex)
                        throw new ParseException("'class', 'struct', 'interface', 'enum', 'delegate', 'event', constructor or destructor declaration, 'implicit operator', 'explicit operator', or type expected.", tok[j].Index);
                    throw;
                }

                if (tok[j].IsBuiltin("this"))
                {
                    // Indexed property
                    var prop = new CsIndexedProperty { Type = type, CustomAttributes = customAttribs };
                    parseModifiers(prop, tok, ref i);
                    if (i != afterModifiers)
                        throw new ParseException("The modifier '{0}' is not valid for indexed properties.".Fmt(tok[i].TokenStr), tok[i].Index);
                    i = j + 1;
                    if (!tok[i].IsBuiltin("["))
                        throw new ParseException("'[' expected.", tok[j].Index);
                    prop.Parameters = parseParameterList(tok, ref i);
                    parsePropertyBody(prop, tok, ref i);
                    return prop;
                }
                else if (tok[j].IsBuiltin("operator"))
                {
                    // Operator overload (except implicit/explicit operator)
                    j++;
                    var overloadableOperators = new[] { "+", "-", "!", "~", "++", "--", "true", "false", "*", "/", "%", "&", "|", "^", "<<", ">>", "==", "!=", "<", ">", "<=", ">=" };
                    if (tok[j].Type != TokenType.Builtin || !overloadableOperators.Contains(tok[j].TokenStr))
                        throw new ParseException("Overloadable operator ({0}) expected.".Fmt(overloadableOperators.Select(o => "'" + o + "'").JoinString(", ")), tok[j].Index);
                    string opStr = tok[j].TokenStr;
                    j++;
                    var parameters = parseParameterList(tok, ref j);
                    CsOperatorOverload op;
                    switch (parameters.Count)
                    {
                        case 1:
                            UnaryOperator unop;
                            switch (opStr)
                            {
                                case "+": unop = UnaryOperator.Plus; break;
                                case "-": unop = UnaryOperator.Minus; break;
                                case "!": unop = UnaryOperator.Not; break;
                                case "~": unop = UnaryOperator.Neg; break;
                                case "++": unop = UnaryOperator.PrefixInc; break;
                                case "--": unop = UnaryOperator.PrefixDec; break;
                                case "true": unop = UnaryOperator.True; break;
                                case "false": unop = UnaryOperator.False; break;
                                default: throw new ParseException("Binary operator must have two parameters. Overloadable unary operators are '+', '-', '!', '~', '++', '--', 'true' and 'false'.", tok[j].Index);
                            }
                            op = new CsUnaryOperatorOverload { CustomAttributes = customAttribs, Parameter = parameters[0], ReturnType = type, Operator = unop };
                            break;

                        case 2:
                            BinaryOperator binop;
                            switch (opStr)
                            {
                                case "+": binop = BinaryOperator.Plus; break;
                                case "-": binop = BinaryOperator.Minus; break;
                                case "*": binop = BinaryOperator.Times; break;
                                case "/": binop = BinaryOperator.Div; break;
                                case "%": binop = BinaryOperator.Mod; break;
                                case "&": binop = BinaryOperator.And; break;
                                case "|": binop = BinaryOperator.Or; break;
                                case "^": binop = BinaryOperator.Xor; break;
                                case "<<": binop = BinaryOperator.Shl; break;
                                case ">>": binop = BinaryOperator.Shr; break;
                                case "==": binop = BinaryOperator.Eq; break;
                                case "!=": binop = BinaryOperator.NotEq; break;
                                case "<": binop = BinaryOperator.Less; break;
                                case ">": binop = BinaryOperator.Greater; break;
                                case "<=": binop = BinaryOperator.LessEq; break;
                                case ">=": binop = BinaryOperator.GreaterEq; break;
                                default: throw new ParseException("Unary operator must have only one parameter. Overloadable binary operators are '+', '-', '*', '/', '%', '&', '|', '^', '<<', '>>', '==', '!=', '<', '>', '<=', and '>='.", tok[j].Index);
                            }
                            op = new CsBinaryOperatorOverload { CustomAttributes = customAttribs, Parameter = parameters[0], SecondParameter = parameters[1], ReturnType = type, Operator = binop };
                            break;

                        default:
                            throw new ParseException("Overloadable operators must have exactly one or two parameters. Use one parameter for unary operators, two for binary operators.", tok[j].Index);
                    }
                    parseModifiers(op, tok, ref i);
                    if (i != afterModifiers)
                        throw new ParseException("The modifier '{0}' is not valid for operator overloads.".Fmt(tok[i].TokenStr), tok[i].Index);
                    i = j;
                    try { op.MethodBody = parseBlock(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsBlock)
                            op.MethodBody = (CsBlock) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, op);
                    }
                    return op;
                }

                string name = tok[j].Identifier("Identifier or 'this' expected.");
                j++;

                if (tok[j].IsBuiltin("{"))
                {
                    var prop = new CsProperty { Type = type, Name = name, CustomAttributes = customAttribs };
                    parseModifiers(prop, tok, ref i);
                    if (i != afterModifiers)
                        throw new ParseException("The modifier '{0}' is not valid for properties.".Fmt(tok[i].TokenStr), tok[i].Index);
                    i = j;
                    parsePropertyBody(prop, tok, ref i);
                    return prop;
                }
                else if (tok[j].IsBuiltin("=") || tok[j].IsBuiltin(";") || tok[j].IsBuiltin(","))
                {
                    CsField fi = new CsField { Type = type, CustomAttributes = customAttribs };
                    parseModifiers(fi, tok, ref i);
                    if (i != afterModifiers)
                        throw new ParseException("The modifier '{0}' is not valid for fields.".Fmt(tok[i].TokenStr), tok[i].Index);
                    i = j;
                    CsExpression initializer = null;
                    try
                    {
                        if (tok[i].IsBuiltin("="))
                        {
                            i++;
                            initializer = parseExpression(tok, ref i);
                        }
                        fi.NamesAndInitializers.Add(Ut.Tuple(name, initializer));
                        while (tok[i].IsBuiltin(","))
                        {
                            i++;
                            name = tok[i].Identifier();
                            initializer = null;
                            i++;
                            if (tok[i].IsBuiltin("="))
                            {
                                i++;
                                initializer = parseExpression(tok, ref i);
                            }
                            fi.NamesAndInitializers.Add(Ut.Tuple(name, initializer));
                        }
                    }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsExpression)
                        {
                            fi.NamesAndInitializers.Add(Ut.Tuple(name, (CsExpression) e.IncompleteResult));
                            throw new ParseException(e.Message, e.Index, fi);
                        }
                        throw;
                    }
                    if (!tok[i].IsBuiltin(";"))
                        throw new ParseException("'=', ',' or ';' expected.", tok[i].Index, fi);
                    i++;
                    return fi;
                }
                else if (tok[j].IsBuiltin("(") || tok[j].IsBuiltin("<") || tok[j].IsBuiltin("."))
                {
                    // If it's "(", it's a method.
                    // If it's "<", it may be a generic method, or it may be a method or property that explicitly implements a member inherited from an abstract type or interface with a generic type argument.
                    // If it's ".", it is a method or property that explicitly implements an inherited abstract or interface member. The method could still be generic.

                    List<Tuple<string, List<CsCustomAttributeGroup>>> genericsAlreadyParsed = null;
                    CsTypeIdentifier implementsFrom = null;

                    if (tok[j].IsBuiltin("<") || tok[j].IsBuiltin("."))
                    {
                        // There are two different cases we need to handle here:
                        // (1) The "<" could be the beginning of the method's generic type parameter declaration, e.g.
                        //         void MyMethod<T>() { }
                        // (2) The "<" could be the beginning of a generic type argument to an interface that the method is trying to implement, e.g.
                        //         IEnumerator<int> IEnumerable<int>.GetEnumerator() { }
                        // In case (1), the generic parameter can have custom attributes, but must otherwise be just an identifier.
                        // In case (2), it can't have custom attributes, but it can have nested generic type arguments or namespaces, e.g. IEnumerable<System.Collections.Generic.List<int>>.
                        // Also, in case (2), it can still be a property rather than a method.
                        // So here's the strategy: We'll see how far we can parse a type identifier. If the method has a generic type parameter with a custom attribute, then this will parse
                        // up to the method name and leave us at the '<'. If the method has only generic type parameters without custom attributes (or none at all), then this will include
                        // the method name and generic type parameters as part of the parsed type identifier. We'll then have to remove it from there.
                        j--;
                        var ty = (CsConcreteTypeIdentifier) parseTypeIdentifier(tok, ref j, typeIdentifierFlags.Lenient);

                        if (tok[j].IsBuiltin(".") && tok[j + 1].IsBuiltin("this"))
                        {
                            // It's an indexed property
                            var prop = new CsIndexedProperty { Type = type, CustomAttributes = customAttribs, ImplementsFrom = ty };
                            parseModifiers(prop, tok, ref i);
                            if (i != afterModifiers)
                                throw new ParseException("The modifier '{0}' is not valid for indexed properties.".Fmt(tok[i].TokenStr), tok[i].Index);
                            i = j + 2;
                            if (!tok[i].IsBuiltin("["))
                                throw new ParseException("'[' expected.", tok[j].Index);
                            prop.Parameters = parseParameterList(tok, ref i);
                            parsePropertyBody(prop, tok, ref i);
                            return prop;
                        }

                        var lastPart = ty.Parts[ty.Parts.Count - 1];
                        ty.Parts.RemoveAt(ty.Parts.Count - 1);
                        if (lastPart.GenericTypeParameters != null)
                        {
                            genericsAlreadyParsed = new List<Tuple<string, List<CsCustomAttributeGroup>>>();
                            foreach (var g in lastPart.GenericTypeParameters)
                            {
                                if (g.IsSingleIdentifier())
                                    genericsAlreadyParsed.Add(Ut.Tuple(g.ToString(), new List<CsCustomAttributeGroup>()));
                                else
                                    throw new ParseException(@"Invalid generic type parameter declaration.", tok[j].Index);
                            }
                        }
                        else if (tok[j].IsBuiltin("<"))
                            genericsAlreadyParsed = parseGenericTypeParameterList(tok, ref j);
                        name = lastPart.Name;
                        if (ty.Parts.Count > 0)
                            implementsFrom = ty;

                        if (tok[j].IsBuiltin("{"))
                        {
                            // It's a property after all
                            if (genericsAlreadyParsed != null)
                                throw new ParseException(@"Properties cannot be generic.", tok[j].Index);
                            var prop = new CsProperty { Type = type, Name = name, CustomAttributes = customAttribs, ImplementsFrom = implementsFrom };
                            parseModifiers(prop, tok, ref i);
                            if (i != afterModifiers)
                                throw new ParseException("The modifier '{0}' is not valid for properties.".Fmt(tok[i].TokenStr), tok[i].Index);
                            i = j;
                            parsePropertyBody(prop, tok, ref i);
                            return prop;
                        }
                    }
                    CsMethod meth = new CsMethod { Type = type, Name = name, CustomAttributes = customAttribs, ImplementsFrom = implementsFrom };
                    parseModifiers(meth, tok, ref i);
                    if (i != afterModifiers)
                        throw new ParseException("The modifier '{0}' is not valid for methods.".Fmt(tok[i].TokenStr), tok[i].Index);
                    i = j;
                    if (genericsAlreadyParsed != null)
                        meth.GenericTypeParameters = genericsAlreadyParsed;
                    else if (tok[i].IsBuiltin("<"))
                        meth.GenericTypeParameters = parseGenericTypeParameterList(tok, ref i);
                    if (!tok[i].IsBuiltin("("))
                        throw new ParseException("'(' expected.", tok[i].Index, meth);
                    try { meth.Parameters = parseParameterList(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is List<CsParameter>)
                            meth.Parameters = (List<CsParameter>) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, meth);
                    }
                    if (tok[i].IsIdentifier("where"))
                    {
                        try { meth.GenericTypeConstraints = parseGenericTypeConstraints(tok, ref i); }
                        catch (ParseException e)
                        {
                            if (e.IncompleteResult is Dictionary<string, List<CsGenericTypeConstraint>>)
                                meth.GenericTypeConstraints = (Dictionary<string, List<CsGenericTypeConstraint>>) e.IncompleteResult;
                            throw new ParseException(e.Message, e.Index, meth);
                        }
                    }
                    if (tok[i].IsBuiltin(";"))
                        i++;
                    else if (tok[i].IsBuiltin("{"))
                    {
                        try { meth.MethodBody = parseBlock(tok, ref i); }
                        catch (ParseException e)
                        {
                            if (e.IncompleteResult is CsBlock)
                                meth.MethodBody = (CsBlock) e.IncompleteResult;
                            throw new ParseException(e.Message, e.Index, meth);
                        }
                    }
                    else
                        throw new ParseException(@"';' or '{' expected.", tok[i].Index, meth);
                    return meth;
                }
                else
                    throw new ParseException("For a field, '=', ',' or ';' expected. For a method, '(' or '<' expected. For a property, '{' expected.", tok[j].Index);
            }
        }

        private static void parsePropertyBody(CsProperty prop, TokenJar tok, ref int i)
        {
            tok[i].Assert("{");
            i++;

            while (!tok[i].IsBuiltin("}"))
            {
                var cAttribs = new List<CsCustomAttributeGroup>();
                while (tok[i].IsBuiltin("["))
                    cAttribs.Add(parseCustomAttributeGroup(tok, ref i, false));
                var m = new CsSimpleMethod { CustomAttributes = cAttribs };
                parseModifiers(m, tok, ref i);
                if (!tok[i].IsIdentifier("get") && !tok[i].IsIdentifier("set"))
                    throw new ParseException("'get' or 'set' expected.", tok[i].Index, prop);
                m.Type = tok[i].TokenStr == "get" ? MethodType.Get : MethodType.Set;
                if (prop.Methods.Any(me => me.Type == m.Type))
                    throw new ParseException("A '{0}' method has already been defined for this member.".Fmt(m.Type), tok[i].Index, prop);
                prop.Methods.Add(m);
                i++;
                if (tok[i].IsBuiltin("{"))
                {
                    try { m.Body = parseBlock(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsBlock)
                            m.Body = (CsBlock) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, prop);
                    }
                }
                else if (tok[i].IsBuiltin(";"))
                    i++;
                else
                    throw new ParseException("'{' or ';' expected.", tok[i].Index, prop);
            }
            i++;
        }

        private static CsCustomAttributeGroup parseCustomAttributeGroup(TokenJar tok, ref int i, bool noNewLine)
        {
            tok[i].Assert("[");
            i++;

            CustomAttributeLocation loc = CustomAttributeLocation.None;
            if ((tok[i].Type == TokenType.Identifier || tok[i].Type == TokenType.Builtin) && tok[i + 1].IsBuiltin(":"))
            {
                switch (tok[i].TokenStr)
                {
                    case "assembly": loc = CustomAttributeLocation.Assembly; break;
                    case "event": loc = CustomAttributeLocation.Event; break;
                    case "field": loc = CustomAttributeLocation.Field; break;
                    case "method": loc = CustomAttributeLocation.Method; break;
                    case "module": loc = CustomAttributeLocation.Module; break;
                    case "param": loc = CustomAttributeLocation.Param; break;
                    case "property": loc = CustomAttributeLocation.Property; break;
                    case "return": loc = CustomAttributeLocation.Return; break;
                    case "type": loc = CustomAttributeLocation.Type; break;
                    case "typevar": loc = CustomAttributeLocation.Typevar; break;
                    default:
                        throw new ParseException("Unrecognized custom attribute location. Valid locations are: 'assembly', 'event', 'field', 'method', 'module', 'param', 'property', 'return', 'type', 'typevar'.", tok[i].Index);
                }
                i += 2;
            }

            List<CsCustomAttribute> group = new List<CsCustomAttribute>();
            while (true)
            {
                var type = parseTypeIdentifier(tok, ref i, 0);
                var attr = new CsCustomAttribute { Type = type };
                group.Add(attr);
                if (tok[i].IsBuiltin("]"))
                {
                    i++;
                    break;
                }
                else if (tok[i].IsBuiltin(","))
                {
                    i++;
                    continue;
                }
                else if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(', ',' or ']' expected.", tok[i].Index);
                i++;
                bool acceptPositional = true;
                bool expectComma = false;
                while (!tok[i].IsBuiltin(")"))
                {
                    if (expectComma)
                    {
                        if (!tok[i].IsBuiltin(","))
                            throw new ParseException("'',' or ')' expected. (1)", tok[i].Index);
                        i++;
                    }
                    expectComma = true;
                    if (tok[i].Type == TokenType.Identifier && tok[i + 1].IsBuiltin("="))
                    {
                        acceptPositional = false;
                        var posName = tok[i].TokenStr;
                        i += 2;
                        var expr = parseExpression(tok, ref i);
                        attr.Named.Add(Ut.Tuple(posName, expr));
                    }
                    else if (acceptPositional)
                        attr.Positional.Add(parseExpression(tok, ref i));
                    else
                        throw new ParseException("Identifier '=' <expression>, or ')' expected.", tok[i].Index);
                }
                i++;
                if (tok[i].IsBuiltin("]"))
                {
                    i++;
                    break;
                }
                else if (tok[i].IsBuiltin(","))
                {
                    i++;
                    continue;
                }
                else
                    throw new ParseException("']' or ',' expected. (1)", tok[i].Index);
            }
            return new CsCustomAttributeGroup { CustomAttributes = group, Location = loc, NoNewLine = noNewLine };
        }

        private static CsBlock parseBlock(TokenJar tok, ref int i)
        {
            tok[i].Assert("{");
            i++;

            CsBlock block = new CsBlock();

            while (!tok[i].IsBuiltin("}"))
            {
                try { block.Statements.Add(parseStatement(tok, ref i)); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsStatement)
                        block.Statements.Add((CsStatement) e.IncompleteResult);
                    throw new ParseException(e.Message, e.Index, block);
                }
            }
            i++;

            return block;
        }

        private static void parseModifiers(CsMember mem, TokenJar tok, ref int i)
        {
            while (true)
            {
                if (tok[i].IsBuiltin("public"))
                    mem.IsPublic = true;
                else if (tok[i].IsBuiltin("protected"))
                    mem.IsProtected = true;
                else if (tok[i].IsBuiltin("private"))
                    mem.IsPrivate = true;
                else if (tok[i].IsBuiltin("internal"))
                    mem.IsInternal = true;
                else if (tok[i].IsBuiltin("new"))
                    mem.IsNew = true;
                else if (tok[i].IsBuiltin("static") && mem is CsMemberLevel2)
                    ((CsMemberLevel2) mem).IsStatic = true;
                else if (tok[i].IsBuiltin("abstract") && mem is CsMemberLevel2)
                    ((CsMemberLevel2) mem).IsAbstract = true;
                else if (tok[i].IsBuiltin("sealed") && mem is CsMemberLevel2)
                    ((CsMemberLevel2) mem).IsSealed = true;
                else if (tok[i].IsBuiltin("virtual") && mem is CsMemberLevel2)
                    ((CsMemberLevel2) mem).IsVirtual = true;
                else if (tok[i].IsBuiltin("override") && mem is CsMemberLevel2)
                    ((CsMemberLevel2) mem).IsOverride = true;
                else if (tok[i].IsBuiltin("static") && mem is CsMultiMember)
                    ((CsMultiMember) mem).IsStatic = true;
                else if (tok[i].IsBuiltin("abstract") && mem is CsEvent)
                    ((CsEvent) mem).IsAbstract = true;
                else if (tok[i].IsBuiltin("sealed") && mem is CsEvent)
                    ((CsEvent) mem).IsSealed = true;
                else if (tok[i].IsBuiltin("virtual") && mem is CsEvent)
                    ((CsEvent) mem).IsVirtual = true;
                else if (tok[i].IsBuiltin("override") && mem is CsEvent)
                    ((CsEvent) mem).IsOverride = true;
                else if (tok[i].IsBuiltin("readonly") && mem is CsField)
                    ((CsField) mem).IsReadonly = true;
                else if (tok[i].IsBuiltin("const") && mem is CsField)
                    ((CsField) mem).IsConst = true;
                else if (tok[i].IsIdentifier("partial") && mem is CsMethod)
                    ((CsMethod) mem).IsPartial = true;
                else if (tok[i].IsBuiltin("extern") && mem is CsMemberLevel2)
                    ((CsMemberLevel2) mem).IsExtern = true;
                else if (tok[i].IsBuiltin("unsafe") && mem is CsMemberLevel2)
                    ((CsMemberLevel2) mem).IsUnsafe = true;
                else if (tok[i].IsBuiltin("unsafe") && mem is CsDelegate)
                    ((CsDelegate) mem).IsUnsafe = true;
                else if (tok[i].IsIdentifier("partial") && mem is CsTypeLevel2)
                    ((CsTypeLevel2) mem).IsPartial = true;
                else if (tok[i].IsBuiltin("static") && mem is CsOperatorOverload)
                    ((CsOperatorOverload) mem).IsStatic = true;
                else if (tok[i].IsBuiltin("static") && mem is CsConstructor)
                    ((CsConstructor) mem).IsStatic = true;
                else
                    break;
                i++;
            }
        }

        private static CsUsing parseUsing(TokenJar tok, ref int i)
        {
            tok[i].Assert("using");
            i++;

            var firstIdent = tok[i].Identifier();
            i++;
            if (tok[i].IsBuiltin("="))
            {
                i++;
                var typeIdent = parseTypeIdentifier(tok, ref i, 0);
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected. (1)", tok[i].Index);
                i++;
                return new CsUsingAlias { Alias = firstIdent, Original = typeIdent };
            }
            else if (tok[i].IsBuiltin("."))
            {
                i++;
                var sb = new StringBuilder(firstIdent);
                sb.Append('.');
                sb.Append(tok[i].Identifier());
                i++;
                while (tok[i].IsBuiltin("."))
                {
                    sb.Append('.');
                    i++;
                    sb.Append(tok[i].Identifier());
                    i++;
                }
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("'.' or ';' expected.", tok[i].Index);
                i++;
                return new CsUsingNamespace { Namespace = sb.ToString() };
            }
            else if (tok[i].IsBuiltin(";"))
            {
                i++;
                return new CsUsingNamespace { Namespace = firstIdent };
            }

            throw new ParseException("'=', '.' or ';' expected.", tok[i].Index);
        }

        private static CsNamespace parseNamespace(TokenJar tok, ref int i)
        {
            tok[i].Assert("namespace");
            i++;

            var sb = new StringBuilder(tok[i].Identifier());
            i++;
            while (tok[i].IsBuiltin("."))
            {
                i++;
                sb.Append('.');
                sb.Append(tok[i].Identifier());
                i++;
            }
            if (!tok[i].IsBuiltin("{"))
                throw new ParseException("'.' or '{' expected.", tok[i].Index);
            i++;

            var ns = new CsNamespace { Name = sb.ToString() };
            try
            {
                while (!tok[i].IsBuiltin("}"))
                {
                    object result = parseMember(tok, ref i, false);
                    if (result is CsUsingAlias)
                        ns.UsingAliases.Add((CsUsingAlias) result);
                    else if (result is CsUsingNamespace)
                        ns.UsingNamespaces.Add((CsUsingNamespace) result);
                    else if (result is CsNamespace)
                        ns.Namespaces.Add((CsNamespace) result);
                    else if (result is CsType)
                        ns.Types.Add((CsType) result);
                    else
                        throw new ParseException("Unexpected element. Expected 'using', 'namespace', or a type declaration.", tok[i].Index);
                }
                i++;
            }
            catch (LexException e)
            {
                throw new ParseException(e.Message, e.Index, ns);
            }
            catch (ParseException e)
            {
                if (e.IncompleteResult is CsUsingAlias)
                    ns.UsingAliases.Add((CsUsingAlias) e.IncompleteResult);
                else if (e.IncompleteResult is CsUsingNamespace)
                    ns.UsingNamespaces.Add((CsUsingNamespace) e.IncompleteResult);
                else if (e.IncompleteResult is CsNamespace)
                    ns.Namespaces.Add((CsNamespace) e.IncompleteResult);
                else if (e.IncompleteResult is CsType)
                    ns.Types.Add((CsType) e.IncompleteResult);
                throw new ParseException(e.Message, e.Index, ns);
            }
            return ns;
        }

        private static CsType parseType(TokenJar tok, ref int i)
        {
            bool isPublic = false;
            bool isProtected = false;
            bool isPrivate = false;
            bool isInternal = false;
            bool isStatic = false;
            bool isAbstract = false;
            bool isSealed = false;
            bool isPartial = false;
            bool isNew = false;
            bool isUnsafe = false;

            while (true)
            {
                if (tok[i].IsBuiltin("public")) isPublic = true;
                else if (tok[i].IsBuiltin("protected")) isProtected = true;
                else if (tok[i].IsBuiltin("private")) isPrivate = true;
                else if (tok[i].IsBuiltin("internal")) isInternal = true;
                else if (tok[i].IsBuiltin("static")) isStatic = true;
                else if (tok[i].IsBuiltin("abstract")) isAbstract = true;
                else if (tok[i].IsBuiltin("sealed")) isSealed = true;
                else if (tok[i].IsIdentifier("partial")) isPartial = true;
                else if (tok[i].IsBuiltin("new")) isNew = true;
                else if (tok[i].IsBuiltin("unsafe")) isUnsafe = true;
                else
                    break;
                i++;
            }

            CsType type;

            if (tok[i].IsBuiltin("class"))
                type = new CsClass { IsAbstract = isAbstract, IsInternal = isInternal, IsPartial = isPartial, IsPrivate = isPrivate, IsProtected = isProtected, IsPublic = isPublic, IsSealed = isSealed, IsStatic = isStatic, IsNew = isNew };
            else if (tok[i].IsBuiltin("struct"))
                type = new CsStruct { IsInternal = isInternal, IsPartial = isPartial, IsPrivate = isPrivate, IsProtected = isProtected, IsPublic = isPublic, IsNew = isNew };
            else if (tok[i].IsBuiltin("interface"))
                type = new CsInterface { IsInternal = isInternal, IsPartial = isPartial, IsPrivate = isPrivate, IsProtected = isProtected, IsPublic = isPublic, IsNew = isNew };
            else if (tok[i].IsBuiltin("enum"))
                type = new CsEnum { IsInternal = isInternal, IsPrivate = isPrivate, IsProtected = isProtected, IsPublic = isPublic, IsNew = isNew };
            else if (tok[i].IsBuiltin("delegate"))
                type = new CsDelegate { IsInternal = isInternal, IsPrivate = isPrivate, IsProtected = isProtected, IsPublic = isPublic, IsNew = isNew, IsUnsafe = isUnsafe };
            else
                throw new ParseException("'class', 'struct', 'interface', 'enum' or 'delegate' expected.", tok[i].Index);
            i++;

            if (type is CsDelegate)
                ((CsDelegate) type).ReturnType = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays);

            type.Name = tok[i].Identifier();
            i++;

            if (tok[i].IsBuiltin("<"))
            {
                if (!(type is CsTypeCanBeGeneric))
                    throw new ParseException("Enums cannot have generic type parameters.", tok[i].Index, type);
                try
                {
                    ((CsTypeCanBeGeneric) type).GenericTypeParameters = parseGenericTypeParameterList(tok, ref i);
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is List<Tuple<string, List<CsCustomAttributeGroup>>>)
                        ((CsTypeCanBeGeneric) type).GenericTypeParameters = (List<Tuple<string, List<CsCustomAttributeGroup>>>) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, type);
                }
            }

            if (type is CsDelegate)
            {
                try
                {
                    ((CsDelegate) type).Parameters = parseParameterList(tok, ref i);
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is List<CsParameter>)
                        ((CsDelegate) type).Parameters = (List<CsParameter>) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, type);
                }
            }

            if (tok[i].IsBuiltin(":"))
            {
                if (type is CsDelegate)
                    throw new ParseException("Delegates cannot have base types.", tok[i].Index, type);
                i++;
                try
                {
                    if (type is CsEnum)
                        ((CsEnum) type).BaseType = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords);
                    else
                    {
                        var type2 = (CsTypeLevel2) type;
                        type2.BaseTypes = new List<CsTypeIdentifier>();
                        type2.BaseTypes.Add(parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords));
                        while (tok[i].IsBuiltin(","))
                        {
                            i++;
                            type2.BaseTypes.Add(parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords));
                        }
                    }
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsTypeIdentifier && type is CsEnum)
                        ((CsEnum) type).BaseType = (CsTypeIdentifier) e.IncompleteResult;
                    else if (e.IncompleteResult is CsTypeIdentifier && type is CsTypeLevel2)
                        ((CsTypeLevel2) type).BaseTypes.Add((CsTypeIdentifier) e.IncompleteResult);
                    throw new ParseException(e.Message, e.Index, type);
                }
            }

            if (tok[i].IsIdentifier("where"))
            {
                if (!(type is CsTypeCanBeGeneric))
                    throw new ParseException("Enums cannot have generic type constraints.", tok[i].Index, type);
                try
                {
                    ((CsTypeCanBeGeneric) type).GenericTypeConstraints = parseGenericTypeConstraints(tok, ref i);
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is Dictionary<string, List<CsGenericTypeConstraint>>)
                        ((CsTypeCanBeGeneric) type).GenericTypeConstraints = (Dictionary<string, List<CsGenericTypeConstraint>>) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, type);
                }
            }

            if (type is CsDelegate)
            {
                if (tok[i].IsBuiltin(";"))
                {
                    i++;
                    return type;
                }
                throw new ParseException("';' expected. (2)", tok[i].Index, type);
            }

            if (!tok[i].IsBuiltin("{"))
                throw new ParseException("'{' expected.", tok[i].Index, type);
            i++;

            if (type is CsEnum)
            {
                var en = (CsEnum) type;
                while (!tok[i].IsBuiltin("}"))
                {
                    string ident;
                    var customAttribs = new List<CsCustomAttributeGroup>();
                    try
                    {
                        while (tok[i].IsBuiltin("["))
                            customAttribs.Add(parseCustomAttributeGroup(tok, ref i, false));
                        ident = tok[i].Identifier("Enum value expected.");
                    }
                    catch (ParseException e)
                    {
                        throw new ParseException(e.Message, e.Index, en);
                    }
                    var val = new CsEnumValue { Name = ident, CustomAttributes = customAttribs };
                    en.EnumValues.Add(val);
                    i++;
                    if (tok[i].IsBuiltin("}"))
                        break;
                    if (tok[i].IsBuiltin("="))
                    {
                        i++;
                        try { val.LiteralValue = parseExpression(tok, ref i); }
                        catch (ParseException e)
                        {
                            if (e.IncompleteResult is CsExpression)
                                val.LiteralValue = (CsExpression) e.IncompleteResult;
                            throw new ParseException(e.Message, e.Index, en);
                        }
                    }
                    if (tok[i].IsBuiltin("}"))
                        break;
                    if (!tok[i].IsBuiltin(","))
                        throw new ParseException(val.LiteralValue == null ? "',', '=' or '}' expected." : "',' or '}' expected.", tok[i].Index, en);
                    i++;
                }
            }
            else
            {
                var type2 = (CsTypeLevel2) type;
                while (!tok[i].IsBuiltin("}"))
                {
                    try
                    {
                        var obj = parseMember(tok, ref i, false);
                        if (obj is CsMember)
                            type2.Members.Add((CsMember) obj);
                        else
                            throw new ParseException("Method, constructor, destructor, property, field, event or nested type expected.", tok[i].Index);
                    }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsMember)
                            type2.Members.Add((CsMember) e.IncompleteResult);
                        throw new ParseException(e.Message, e.Index, type);
                    }
                }
                if (!tok[i].IsBuiltin("}"))
                    throw new ParseException("Method, property, field, event, nested type or '}' expected.", tok[i].Index, type);
            }
            i++;

            // Skip optional ';' after type declaration
            if (tok[i].IsBuiltin(";"))
                i++;

            return type;
        }

        private static Dictionary<string, List<CsGenericTypeConstraint>> parseGenericTypeConstraints(TokenJar tok, ref int i)
        {
            var ret = new Dictionary<string, List<CsGenericTypeConstraint>>();
            while (tok[i].IsIdentifier("where"))
            {
                i++;
                string genericParameter;
                try { genericParameter = tok[i].Identifier(); }
                catch (ParseException e) { throw new ParseException(e.Message, e.Index, ret); }
                if (ret.ContainsKey(genericParameter))
                    throw new ParseException("A constraint clause has already been specified for type parameter '{0}'. All of the constraints for a type parameter must be specified in a single where clause.".Fmt(genericParameter), tok[i].Index, ret);
                i++;
                if (!tok[i].IsBuiltin(":"))
                    throw new ParseException("':' expected.", tok[i].Index, ret);

                do
                {
                    i++;
                    if (tok[i].IsBuiltin("new") && tok.IndexExists(i + 2) && tok[i + 1].IsBuiltin("(") && tok[i + 2].IsBuiltin(")"))
                    {
                        ret.AddSafe(genericParameter, new CsGenericTypeConstraintNew());
                        i += 3;
                    }
                    else if (tok[i].IsBuiltin("class"))
                    {
                        ret.AddSafe(genericParameter, new CsGenericTypeConstraintClass());
                        i++;
                    }
                    else if (tok[i].IsBuiltin("struct"))
                    {
                        ret.AddSafe(genericParameter, new CsGenericTypeConstraintStruct());
                        i++;
                    }
                    else if (tok[i].Type != TokenType.Identifier)
                        throw new ParseException("Generic type constraint ('new()', 'class', 'struct', or type identifier) expected.", tok[i].Index, ret);
                    else
                    {
                        try
                        {
                            CsTypeIdentifier ident = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords);
                            ret.AddSafe(genericParameter, new CsGenericTypeConstraintBaseClass { BaseClass = ident });
                        }
                        catch (ParseException e)
                        {
                            if (e.IncompleteResult is CsTypeIdentifier)
                                ret.AddSafe(genericParameter, new CsGenericTypeConstraintBaseClass { BaseClass = (CsTypeIdentifier) e.IncompleteResult });
                            throw new ParseException(e.Message, e.Index, ret);
                        }
                    }
                }
                while (tok[i].IsBuiltin(","));
            }
            return ret;
        }

        private static List<CsParameter> parseParameterList(TokenJar tok, ref int i)
        {
            bool square = tok[i].IsBuiltin("[");

            if (!square && !tok[i].IsBuiltin("("))
                throw new ParseException("'(' + parameter list + ')' expected.", tok[i].Index);

            List<CsParameter> ret = new List<CsParameter>();
            if (tok[i + 1].IsBuiltin(square ? "]" : ")"))
            {
                i += 2;
                return ret;
            }

            do
            {
                i++;
                try { ret.Add(parseParameter(tok, ref i)); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsParameter)
                        ret.Add((CsParameter) e.IncompleteResult);
                    throw new ParseException(e.Message, e.Index, ret);
                }
            }
            while (tok[i].IsBuiltin(","));

            if (!tok[i].IsBuiltin(square ? "]" : ")"))
                throw new ParseException(square ? "']' or ',' expected. (2)" : "')' or ',' expected.", tok[i].Index, ret);
            i++;

            return ret;
        }

        private static CsParameter parseParameter(TokenJar tok, ref int i)
        {
            var customAttribs = new List<CsCustomAttributeGroup>();
            while (tok[i].IsBuiltin("["))
                customAttribs.Add(parseCustomAttributeGroup(tok, ref i, true));

            bool isThis = false, isOut = false, isRef = false, isParams = false;
            if (tok[i].IsBuiltin("this"))
            {
                isThis = true;
                i++;
            }
            if (tok[i].IsBuiltin("out"))
            {
                isOut = true;
                i++;
            }
            if (tok[i].IsBuiltin("ref"))
            {
                isRef = true;
                i++;
            }
            if (tok[i].IsBuiltin("params"))
            {
                isParams = true;
                i++;
            }
            var type = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays);
            var name = tok[i].Identifier();
            i++;
            return new CsParameter { Type = type, Name = name, IsThis = isThis, IsOut = isOut, IsRef = isRef, IsParams = isParams, CustomAttributes = customAttribs };
        }

        private static List<Tuple<string, List<CsCustomAttributeGroup>>> parseGenericTypeParameterList(TokenJar tok, ref int i)
        {
            tok[i].Assert("<");
            var genericTypeParameters = new List<Tuple<string, List<CsCustomAttributeGroup>>>();
            while (true)
            {
                i++;
                var customAttribs = new List<CsCustomAttributeGroup>();
                while (tok[i].IsBuiltin("["))
                    customAttribs.Add(parseCustomAttributeGroup(tok, ref i, true));
                var name = tok[i].Identifier();
                i++;
                genericTypeParameters.Add(Ut.Tuple(name, customAttribs));
                if (tok[i].IsBuiltin(","))
                    continue;
                else if (tok[i].IsBuiltin(">"))
                    break;
                throw new ParseException("',' or '>' expected. (1)", tok[i].Index, genericTypeParameters);
            }
            i++;
            return genericTypeParameters;
        }

        [Flags]
        private enum typeIdentifierFlags
        {
            AllowKeywords = 1,
            AllowEmptyGenerics = 2,
            AllowSuffixes = 4,   // '?' for nullable, '*' for pointers
            AllowArrays = 8,
            Lenient = 16,
        }

        private static string[] builtinTypes = new[] { "bool", "byte", "char", "decimal", "double", "float", "int", "long", "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "void" };

        private static CsTypeIdentifier parseTypeIdentifier(TokenJar tok, ref int i, typeIdentifierFlags flags)
        {
            var ty = new CsConcreteTypeIdentifier();
            if (tok[i].IsIdentifier("global") && tok[i + 1].IsBuiltin("::"))
            {
                ty.HasGlobal = true;
                i += 2;
            }

            var j = i;
            while (true)
            {
                var part = new CsConcreteTypeIdentifierPart();
                if (tok[j].Type == TokenType.Builtin && (flags & typeIdentifierFlags.AllowKeywords) == typeIdentifierFlags.AllowKeywords && builtinTypes.Contains(tok[j].TokenStr))
                    part.Name = tok[j].TokenStr;
                else if (ty.Parts.Count > 0 && (flags & typeIdentifierFlags.Lenient) != 0 && tok[j].Type != TokenType.Identifier)
                    return ty;
                else
                    part.Name = tok[j].Identifier("Type expected.");
                j++;
                ty.Parts.Add(part);
                i = j;

                if (tok[j].IsBuiltin("<"))
                {
                    part.GenericTypeParameters = new List<CsTypeIdentifier>();
                    j++;
                    if ((flags & typeIdentifierFlags.AllowEmptyGenerics) != 0 && (tok[j].IsBuiltin(",") || tok[j].IsBuiltin(">")))
                    {
                        part.GenericTypeParameters.Add(new CsEmptyGenericTypeIdentifier());
                        while (tok[j].IsBuiltin(","))
                        {
                            j++;
                            part.GenericTypeParameters.Add(new CsEmptyGenericTypeIdentifier());
                        }
                        if (!tok.Has('>', j))
                            throw new ParseException("',' or '>' expected. (2)", tok[j].Index, ty);
                        tok.Split(j);
                        j++;
                    }
                    else
                    {
                        try
                        {
                            part.GenericTypeParameters.Add(parseTypeIdentifier(tok, ref j, flags | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays | typeIdentifierFlags.AllowKeywords));
                            while (tok[j].IsBuiltin(","))
                            {
                                j++;
                                part.GenericTypeParameters.Add(parseTypeIdentifier(tok, ref j, flags | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays | typeIdentifierFlags.AllowKeywords));
                            }
                            if (!tok.Has('>', j))
                                throw new ParseException("',' or '>' expected. (3)", tok[j].Index, ty);
                            tok.Split(j);
                            j++;
                        }
                        catch (ParseException)
                        {
                            if ((flags & typeIdentifierFlags.Lenient) == 0)
                            {
                                part.GenericTypeParameters = null;
                                return ty;
                            }
                            throw;
                        }
                    }
                }
                i = j;
                if (tok[j].IsBuiltin("."))
                    j++;
                else
                    break;
            }

            CsTypeIdentifier ret = ty;
            i = j;

            try
            {
                if ((flags & typeIdentifierFlags.AllowSuffixes) != 0)
                {
                    while (tok[j].IsBuiltin("*"))
                    {
                        j++;
                        ret = new CsPointerTypeIdentifier { InnerType = ret };
                    }
                    if (tok[j].IsBuiltin("?"))
                    {
                        j++;
                        ret = new CsNullableTypeIdentifier { InnerType = ret };
                    }
                }
                i = j;
                if ((flags & typeIdentifierFlags.AllowArrays) != 0)
                {
                    var arrayRanks = new List<int>();
                    while (tok[j].IsBuiltin("[") && (tok[j + 1].IsBuiltin("]") || tok[j + 1].IsBuiltin(",")))
                    {
                        j++;
                        int num = 1;
                        while (tok[j].IsBuiltin(","))
                        {
                            num++;
                            j++;
                        }
                        if (!tok[j].IsBuiltin("]"))
                        {
                            if ((flags & typeIdentifierFlags.Lenient) == 0)
                                throw new ParseException("',' or ']' expected.", tok[j].Index, ret);
                            if (arrayRanks.Count > 0)
                                ret = new CsArrayTypeIdentifier { ArrayRanks = arrayRanks, InnerType = ret };
                            return ret;
                        }
                        j++;
                        i = j;
                        arrayRanks.Add(num);
                    }
                    if (arrayRanks.Count > 0)
                        ret = new CsArrayTypeIdentifier { ArrayRanks = arrayRanks, InnerType = ret };
                }
            }
            catch (ParseException e)
            {
                if ((flags & typeIdentifierFlags.Lenient) != 0)
                    return ret;
                if (e.IncompleteResult is CsTypeIdentifier)
                    throw;
                throw new ParseException(e.Message, e.Index, ret);
            }
            i = j;
            return ret;
        }

        private static CsStatement parseStatement(TokenJar tok, ref int i)
        {
            if (tok[i].IsBuiltin(";"))
            {
                i++;
                return new CsEmptyStatement();
            }
            else if (tok[i].IsBuiltin("return") || tok[i].IsBuiltin("throw"))
            {
                var ret = tok[i].IsBuiltin("return") ? (CsOptionalExpressionStatement) new CsReturnStatement() : new CsThrowStatement();
                i++;
                if (tok[i].IsBuiltin(";"))
                {
                    i++;
                    return ret;
                }
                try { ret.Expression = parseExpression(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        ret.Expression = (CsExpression) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, ret);
                }
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected. (3)", tok[i].Index, ret);
                i++;
                return ret;
            }
            else if (tok[i].IsBuiltin("checked") || tok[i].IsBuiltin("unchecked") || tok[i].IsBuiltin("unsafe"))
            {
                CsBlockStatement ret = tok[i].IsBuiltin("checked") ? new CsCheckedStatement() : tok[i].IsBuiltin("unchecked") ? (CsBlockStatement) new CsUncheckedStatement() : new CsUnsafeStatement();
                i++;
                ret.Block = parseBlock(tok, ref i);
                return ret;
            }
            else if (tok[i].IsBuiltin("switch"))
            {
                var sw = new CsSwitchStatement();
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index, sw);
                i++;
                try { sw.SwitchOn = parseExpression(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        sw.SwitchOn = (CsExpression) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, sw);
                }
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index, sw);
                i++;
                if (!tok[i].IsBuiltin("{"))
                    throw new ParseException("'{' expected.", tok[i].Index, sw);
                i++;
                CsCaseLabel cas = null;
                while (!tok[i].IsBuiltin("}"))
                {
                    if (tok[i].IsBuiltin("case") || (tok[i].IsBuiltin("default") && !tok[i + 1].IsBuiltin("(")))
                    {
                        bool isDef = tok[i].IsBuiltin("default");
                        i++;
                        if (cas == null || cas.Statements != null)
                        {
                            cas = new CsCaseLabel();
                            sw.Cases.Add(cas);
                        }
                        cas.CaseValues.Add(isDef ? null : parseExpression(tok, ref i));
                        if (!tok[i].IsBuiltin(":"))
                            throw new ParseException("':' expected.", tok[i].Index, sw);
                        i++;
                    }
                    else if (cas == null)
                        throw new ParseException("'case' <expression> or 'default:' expected.", tok[i].Index, sw);
                    else
                    {
                        try
                        {
                            if (cas.Statements == null)
                                cas.Statements = new List<CsStatement>();
                            var stat = parseStatement(tok, ref i);
                            cas.Statements.Add(stat);
                        }
                        catch (ParseException e)
                        {
                            if (e.IncompleteResult is CsStatement)
                                cas.Statements.Add((CsStatement) e.IncompleteResult);
                            throw new ParseException(e.Message, e.Index, sw);
                        }
                    }
                }
                i++;
                return sw;
            }
            else if (tok[i].IsBuiltin("foreach"))
            {
                var fore = new CsForeachStatement();
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index);
                i++;
                if (tok[i].Type == TokenType.Identifier && tok[i + 1].IsBuiltin("in"))
                {
                    fore.VariableName = tok[i].TokenStr;
                    i += 2;
                }
                else
                {
                    fore.VariableType = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowArrays | typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes);
                    fore.VariableName = tok[i].Identifier();
                    i++;
                    if (!tok[i].IsBuiltin("in"))
                        throw new ParseException("'in' expected.", tok[i].Index);
                    i++;
                }
                fore.LoopExpression = parseExpression(tok, ref i);
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index);
                i++;
                try { fore.Body = parseStatement(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsStatement)
                    {
                        fore.Body = (CsStatement) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, fore);
                    }
                    throw;
                }
                return fore;
            }
            else if (tok[i].IsBuiltin("for"))
            {
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index);
                i++;
                var fore = new CsForStatement();
                if (tok[i].IsBuiltin(";"))
                    fore.InitializationStatement = new CsEmptyStatement();
                else
                    fore.InitializationStatement = parseVariableDeclarationOrExpressionStatement(tok, ref i);
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected.", tok[i].Index);
                i++;
                if (!tok[i].IsBuiltin(";"))
                {
                    fore.TerminationCondition = parseExpression(tok, ref i);
                    if (!tok[i].IsBuiltin(";"))
                        throw new ParseException("';' expected.", tok[i].Index);
                }
                i++;
                if (!tok[i].IsBuiltin(")"))
                {
                    fore.LoopExpression = parseExpression(tok, ref i);
                    if (!tok[i].IsBuiltin(")"))
                        throw new ParseException("')' expected.", tok[i].Index);
                }
                i++;

                try { fore.Body = parseStatement(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsStatement)
                    {
                        fore.Body = (CsStatement) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, fore);
                    }
                    throw;
                }
                return fore;
            }
            else if (tok[i].IsBuiltin("using"))
            {
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index);
                i++;
                var usin = new CsUsingStatement { InitializationStatement = parseVariableDeclarationOrExpressionStatement(tok, ref i) };
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index);
                i++;

                try { usin.Body = parseStatement(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsStatement)
                    {
                        usin.Body = (CsStatement) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, usin);
                    }
                    throw;
                }
                return usin;
            }
            else if (tok[i].IsBuiltin("fixed"))
            {
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index);
                i++;
                var k = i;
                var fixd = new CsFixedStatement { InitializationStatement = parseVariableDeclarationOrExpressionStatement(tok, ref i) };
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index);
                i++;

                if (!(fixd.InitializationStatement is CsVariableDeclarationStatement) || !(((CsVariableDeclarationStatement) fixd.InitializationStatement).Type is CsPointerTypeIdentifier))
                    throw new ParseException("'fixed' statement requires a variable declaration for a pointer-typed variable.", tok[k].Index);

                try { fixd.Body = parseStatement(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsStatement)
                    {
                        fixd.Body = (CsStatement) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, fixd);
                    }
                    throw;
                }
                return fixd;
            }
            else if (tok[i].IsBuiltin("if"))
            {
                var ifs = new CsIfStatement();
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index);
                i++;
                ifs.IfExpression = parseExpression(tok, ref i);
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index);
                i++;
                try { ifs.Statement = parseStatement(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsStatement)
                        ifs.Statement = (CsStatement) e.IncompleteResult;
                    else
                        ifs.Statement = new CsEmptyStatement();
                    throw new ParseException(e.Message, e.Index, ifs);
                }
                if (tok[i].IsBuiltin("else"))
                {
                    i++;
                    try { ifs.ElseStatement = parseStatement(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsStatement)
                            ifs.ElseStatement = (CsStatement) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, ifs);
                    }
                }
                return ifs;
            }
            else if (tok[i].IsBuiltin("while") || tok[i].IsBuiltin("lock"))
            {
                var stat = tok[i].IsBuiltin("while") ? (CsExpressionBlockStatement) new CsWhileStatement() : new CsLockStatement();
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected.", tok[i].Index);
                i++;
                stat.Expression = parseExpression(tok, ref i);
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index);
                i++;
                try { stat.Statement = parseStatement(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsStatement)
                    {
                        stat.Statement = (CsStatement) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, stat);
                    }
                    throw;
                }
                return stat;
            }
            else if (tok[i].IsBuiltin("do"))
            {
                var dow = new CsDoWhileStatement();
                i++;
                dow.Statement = parseStatement(tok, ref i);
                if (!tok[i].IsBuiltin("while") || !tok[i + 1].IsBuiltin("("))
                    throw new ParseException("'while' followed by '(' expected.", tok[i].Index);
                i += 2;
                dow.WhileExpression = parseExpression(tok, ref i);
                if (!tok[i].IsBuiltin(")") || !tok[i + 1].IsBuiltin(";"))
                    throw new ParseException("')' followed by ';' expected.", tok[i].Index, dow);
                i += 2;
                return dow;
            }
            else if (tok[i].IsBuiltin("try"))
            {
                i++;
                if (!tok[i].IsBuiltin("{"))
                    throw new ParseException("'{' expected after 'try'.", tok[i].Index);
                var tr = new CsTryStatement { Block = parseBlock(tok, ref i) };
                if (!tok[i].IsBuiltin("catch") && !tok[i].IsBuiltin("finally"))
                    throw new ParseException("'catch' or 'finally' expected after 'try' block.", tok[i].Index);
                while (tok[i].IsBuiltin("catch"))
                {
                    i++;
                    var ctch = new CsCatchClause();
                    tr.Catches.Add(ctch);
                    if (tok[i].IsBuiltin("("))
                    {
                        i++;
                        ctch.Type = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowArrays | typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes);
                        if (tok[i].Type == TokenType.Identifier)
                        {
                            ctch.Name = tok[i].TokenStr;
                            i++;
                        }
                        if (!tok[i].IsBuiltin(")"))
                            throw new ParseException("')' expected.", tok[i].Index);
                        i++;
                    }
                    if (!tok[i].IsBuiltin("{"))
                        throw new ParseException("'{' expected.", tok[i].Index);
                    try { ctch.Block = parseBlock(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsBlock)
                            ctch.Block = (CsBlock) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, tr);
                    }
                }
                if (tok[i].IsBuiltin("finally"))
                {
                    i++;
                    if (!tok[i].IsBuiltin("{"))
                        throw new ParseException("'{' expected after 'finally'.", tok[i].Index);
                    try { tr.Finally = parseBlock(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsBlock)
                            tr.Finally = (CsBlock) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, tr);
                    }
                }
                return tr;
            }
            else if (tok[i].IsBuiltin("goto"))
            {
                i++;
                var name = tok[i].Identifier();
                i++;
                var got = new CsGotoStatement { Label = name };
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected.", tok[i].Index, got);
                i++;
                return got;
            }
            else if (tok[i].IsBuiltin("continue"))
            {
                i++;
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected.", tok[i].Index, new CsContinueStatement());
                i++;
                return new CsContinueStatement();
            }
            else if (tok[i].IsBuiltin("break"))
            {
                i++;
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected.", tok[i].Index, new CsBreakStatement());
                i++;
                return new CsBreakStatement();
            }
            else if (tok[i].IsIdentifier("yield") && tok[i + 1].IsBuiltin("break"))
            {
                i += 2;
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected.", tok[i].Index, new CsYieldBreakStatement());
                i++;
                return new CsYieldBreakStatement();
            }
            else if (tok[i].IsIdentifier("yield") && tok[i + 1].IsBuiltin("return"))
            {
                i += 2;
                CsExpression expr;
                try { expr = parseExpression(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsYieldReturnStatement { Expression = (CsExpression) e.IncompleteResult });
                    throw;
                }
                var yieldreturn = new CsYieldReturnStatement { Expression = expr };
                if (!tok[i].IsBuiltin(";"))
                    throw new ParseException("';' expected.", tok[i].Index, yieldreturn);
                i++;
                return yieldreturn;
            }
            else if (tok[i].IsBuiltin("{"))
                return parseBlock(tok, ref i);
            else if (tok[i].Type == TokenType.Identifier && tok[i + 1].IsBuiltin(":"))
            {
                string name = tok[i].TokenStr;
                i += 2;
                CsStatement inner = parseStatement(tok, ref i);
                if (inner.GotoLabels == null)
                    inner.GotoLabels = new List<string> { name };
                else
                    inner.GotoLabels.Insert(0, name);
                return inner;
            }

            // Slight hack: parseVariableDeclarationOrExpressionStatement() does not consume the trailing ';' of the statement.
            // This is so that the same method can be used in parsing the 'using' statement, which ends with a ')' rather than ';'.
            var stmt = parseVariableDeclarationOrExpressionStatement(tok, ref i);
            if (!tok[i].IsBuiltin(";"))
                throw new ParseException("';' expected.", tok[i].Index, stmt);
            i++;
            return stmt;
        }

        private static CsStatement parseVariableDeclarationOrExpressionStatement(TokenJar tok, ref int i)
        {
            bool isConst = false;

            if (tok[i].IsBuiltin("const"))
            {
                isConst = true;
                i++;
            }

            // See if the beginning of this statement is a type identifier followed by a variable name, in which case parse it as a variable declaration.
            CsTypeIdentifier declType = null;
            var j = i;
            try
            {
                var ty = parseTypeIdentifier(tok, ref j, typeIdentifierFlags.AllowArrays | typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes);
                if (tok[j].Type == TokenType.Identifier)
                    declType = ty;
            }
            catch (ParseException) { }

            // If this looks like a valid variable declaration, continue parsing it.
            if (declType != null)
            {
                i = j - 1;
                var decl = new CsVariableDeclarationStatement { Type = declType, IsConst = isConst };
                string name = null;
                try
                {
                    do
                    {
                        i++;
                        name = tok[i].Identifier();
                        i++;
                        CsExpression expr = null;
                        if (tok[i].IsBuiltin("="))
                        {
                            i++;
                            expr = parseExpression(tok, ref i);
                        }
                        decl.NamesAndInitializers.Add(Ut.Tuple(name, expr));
                    }
                    while (tok[i].IsBuiltin(","));
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression && name != null)
                        decl.NamesAndInitializers.Add(Ut.Tuple(name, (CsExpression) e.IncompleteResult));
                    throw new ParseException(e.Message, e.Index, decl);
                }

                // This function does not consume the trailing ';' of the statement, so that 'using' can use it (where the statement ends with a ')' instead).
                return decl;
            }

            if (isConst)
                throw new ParseException("Expected type + variable declaration after 'const'.", tok[i].Index);

            // Finally, the only remaining possible way for it to be a valid statement is by being an expression.
            var exprStat = new CsExpressionStatement();
            int index = tok[i].Index;
            try { exprStat.Expression = parseExpression(tok, ref i); }
            catch (ParseException e)
            {
                var msg = index == e.Index ? @"Invalid statement." : e.Message;
                if (e.IncompleteResult is CsExpression)
                {
                    exprStat.Expression = (CsExpression) e.IncompleteResult;
                    throw new ParseException(msg, e.Index, exprStat);
                }
                throw new ParseException(msg, e.Index, e.IncompleteResult);
            }

            // This function does not consume the trailing ';' of the statement, so that 'using' can use it (where the statement ends with a ')' instead).
            return exprStat;
        }

        private static string[] assignmentOperators = new[] { "=", "*=", "/=", "%=", "+=", "-=", "<<=", ">>=", "&=", "^=", "|=" };

        private static CsExpression parseExpression(TokenJar tok, ref int i)
        {
            var left = parseExpressionConditional(tok, ref i);
            if (tok[i].Type == TokenType.Builtin && assignmentOperators.Contains(tok[i].TokenStr))
            {
                AssignmentOperator type;
                switch (tok[i].TokenStr)
                {
                    case "=": type = AssignmentOperator.Eq; break;
                    case "*=": type = AssignmentOperator.TimesEq; break;
                    case "/=": type = AssignmentOperator.DivEq; break;
                    case "%=": type = AssignmentOperator.ModEq; break;
                    case "+=": type = AssignmentOperator.PlusEq; break;
                    case "-=": type = AssignmentOperator.MinusEq; break;
                    case "<<=": type = AssignmentOperator.ShlEq; break;
                    case ">>=": type = AssignmentOperator.ShrEq; break;
                    case "&=": type = AssignmentOperator.AndEq; break;
                    case "^=": type = AssignmentOperator.XorEq; break;
                    case "|=": type = AssignmentOperator.OrEq; break;
                    default: throw new ParseException("Unknown assigment operator.", tok[i].Index, left);
                }
                i++;
                var right = parseExpression(tok, ref i);
                return new CsAssignmentExpression { Left = left, Right = right, Operator = type };
            }
            return left;
        }
        private static CsExpression parseExpressionConditional(TokenJar tok, ref int i)
        {
            var left = parseExpressionCoaslesce(tok, ref i);
            bool haveQ = false;
            if (tok[i].IsBuiltin("?"))
            {
                haveQ = true;
                i++;
            }
            else
            {
                // This is very hacky, but I couldn't find a better way. We need to handle the following cases:
                //     myObj is int ? 5 : 1
                //     myObj is int? ? 5 : 1
                // We are fine for the second case, but in order to get that second case to work, we had to parse 'int?' as a type.
                // Therefore, if the input is actually the first case (which is more common), we have to track down that type and remove the question mark from it.
                var analyse = left;
                while (analyse is CsBinaryOperatorExpression)
                    analyse = ((CsBinaryOperatorExpression) analyse).Right;
                if (analyse is CsBinaryTypeOperatorExpression)
                {
                    var bin = (CsBinaryTypeOperatorExpression) analyse;
                    if (bin.Right is CsNullableTypeIdentifier)
                    {
                        bin.Right = ((CsNullableTypeIdentifier) bin.Right).InnerType;
                        haveQ = true;
                    }
                }
            }

            if (haveQ)
            {
                CsExpression middle;
                try { middle = parseExpression(tok, ref i); }
                catch (ParseException e) { throw new ParseException(e.Message, e.Index, left); }
                if (!tok[i].IsBuiltin(":"))
                    throw new ParseException("Unterminated conditional operator. ':' expected.", tok[i].Index, left);
                i++;
                try { return new CsConditionalExpression { Left = left, Middle = middle, Right = parseExpression(tok, ref i) }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsConditionalExpression { Left = left, Middle = middle, Right = (CsExpression) e.IncompleteResult });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionCoaslesce(TokenJar tok, ref int i)
        {
            var left = parseExpressionBoolOr(tok, ref i);
            while (tok[i].IsBuiltin("??"))
            {
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionBoolOr(tok, ref i), Operator = BinaryOperator.Coalesce }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = BinaryOperator.Coalesce });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionBoolOr(TokenJar tok, ref int i)
        {
            var left = parseExpressionBoolAnd(tok, ref i);
            while (tok[i].IsBuiltin("||"))
            {
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionBoolAnd(tok, ref i), Operator = BinaryOperator.OrOr }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = BinaryOperator.OrOr });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionBoolAnd(TokenJar tok, ref int i)
        {
            var left = parseExpressionLogicalOr(tok, ref i);
            while (tok[i].IsBuiltin("&&"))
            {
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionLogicalOr(tok, ref i), Operator = BinaryOperator.AndAnd }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = BinaryOperator.AndAnd });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionLogicalOr(TokenJar tok, ref int i)
        {
            var left = parseExpressionLogicalXor(tok, ref i);
            while (tok[i].IsBuiltin("|"))
            {
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionLogicalXor(tok, ref i), Operator = BinaryOperator.Or }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = BinaryOperator.Or });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionLogicalXor(TokenJar tok, ref int i)
        {
            var left = parseExpressionLogicalAnd(tok, ref i);
            while (tok[i].IsBuiltin("^"))
            {
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionLogicalAnd(tok, ref i), Operator = BinaryOperator.Xor }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = BinaryOperator.Xor });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionLogicalAnd(TokenJar tok, ref int i)
        {
            var left = parseExpressionEquality(tok, ref i);
            while (tok[i].IsBuiltin("&"))
            {
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionEquality(tok, ref i), Operator = BinaryOperator.And }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = BinaryOperator.And });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionEquality(TokenJar tok, ref int i)
        {
            var left = parseExpressionRelational(tok, ref i);
            while (tok[i].IsBuiltin("==") || tok[i].IsBuiltin("!="))
            {
                BinaryOperator op = tok[i].IsBuiltin("==") ? BinaryOperator.Eq : BinaryOperator.NotEq;
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionRelational(tok, ref i), Operator = op }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = op });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionRelational(TokenJar tok, ref int i)
        {
            var left = parseExpressionShift(tok, ref i);
            while (tok[i].IsBuiltin("<") || tok[i].IsBuiltin(">") || tok[i].IsBuiltin("<=") || tok[i].IsBuiltin(">=") || tok[i].IsBuiltin("is") || tok[i].IsBuiltin("as"))
            {
                if (tok[i].IsBuiltin("is") || tok[i].IsBuiltin("as"))
                {
                    var op = tok[i].IsBuiltin("is") ? BinaryTypeOperator.Is : BinaryTypeOperator.As;
                    i++;
                    try { return new CsBinaryTypeOperatorExpression { Left = left, Right = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays), Operator = op }; }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsTypeIdentifier)
                            throw new ParseException(e.Message, e.Index, new CsBinaryTypeOperatorExpression { Left = left, Right = (CsTypeIdentifier) e.IncompleteResult, Operator = op });
                        throw new ParseException(e.Message, e.Index, left);
                    }
                }
                else
                {
                    BinaryOperator op = tok[i].IsBuiltin("<") ? BinaryOperator.Less : tok[i].IsBuiltin("<=") ? BinaryOperator.LessEq : tok[i].IsBuiltin(">") ? BinaryOperator.Greater : BinaryOperator.GreaterEq;
                    i++;
                    try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionShift(tok, ref i), Operator = op }; }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsExpression)
                            throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = op });
                        throw new ParseException(e.Message, e.Index, left);
                    }
                }
            }
            return left;
        }
        private static CsExpression parseExpressionShift(TokenJar tok, ref int i)
        {
            var left = parseExpressionAdditive(tok, ref i);
            while (tok[i].IsBuiltin("<<") || tok[i].IsBuiltin(">>"))
            {
                BinaryOperator op = tok[i].IsBuiltin("<<") ? BinaryOperator.Shl : BinaryOperator.Shr;
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionAdditive(tok, ref i), Operator = op }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = op });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionAdditive(TokenJar tok, ref int i)
        {
            var left = parseExpressionMultiplicative(tok, ref i);
            while (tok[i].IsBuiltin("+") || tok[i].IsBuiltin("-"))
            {
                BinaryOperator op = tok[i].IsBuiltin("+") ? BinaryOperator.Plus : BinaryOperator.Minus;
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionMultiplicative(tok, ref i), Operator = op }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = op });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionMultiplicative(TokenJar tok, ref int i)
        {
            var left = parseExpressionUnary(tok, ref i);
            while (tok[i].IsBuiltin("*") || tok[i].IsBuiltin("/") || tok[i].IsBuiltin("%"))
            {
                BinaryOperator op = tok[i].IsBuiltin("*") ? BinaryOperator.Times : tok[i].IsBuiltin("/") ? BinaryOperator.Div : BinaryOperator.Mod;
                i++;
                try { left = new CsBinaryOperatorExpression { Left = left, Right = parseExpressionUnary(tok, ref i), Operator = op }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        throw new ParseException(e.Message, e.Index, new CsBinaryOperatorExpression { Left = left, Right = (CsExpression) e.IncompleteResult, Operator = op });
                    throw new ParseException(e.Message, e.Index, left);
                }
            }
            return left;
        }
        private static CsExpression parseExpressionUnary(TokenJar tok, ref int i)
        {
            if (tok[i].Type != TokenType.Builtin)
                return parseExpressionPrimary(tok, ref i);

            UnaryOperator op;
            switch (tok[i].TokenStr)
            {
                case "+": op = UnaryOperator.Plus; break;
                case "-": op = UnaryOperator.Minus; break;
                case "*": op = UnaryOperator.PointerDeref; break;
                case "&": op = UnaryOperator.AddressOf; break;
                case "!": op = UnaryOperator.Not; break;
                case "~": op = UnaryOperator.Neg; break;
                case "++": op = UnaryOperator.PrefixInc; break;
                case "--": op = UnaryOperator.PrefixDec; break;
                default:
                    return parseExpressionPrimary(tok, ref i);
            }
            i++;
            var operand = parseExpressionUnary(tok, ref i);
            return new CsUnaryOperatorExpression { Operand = operand, Operator = op };
        }
        private static CsExpression parseExpressionPrimary(TokenJar tok, ref int i)
        {
            var left = parseExpressionIdentifierOrKeyword(tok, ref i);
            while (tok[i].IsBuiltin(".") || tok[i].IsBuiltin("(") || tok[i].IsBuiltin("[") || tok[i].IsBuiltin("++") || tok[i].IsBuiltin("--"))
            {
                if (tok[i].IsBuiltin("."))
                {
                    i++;
                    try { left = new CsMemberAccessExpression { Left = left, Right = parseExpressionIdentifier(tok, ref i) }; }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsExpression)
                            throw new ParseException(e.Message, e.Index, new CsMemberAccessExpression { Left = left, Right = (CsExpression) e.IncompleteResult });
                        throw new ParseException(e.Message, e.Index, left);
                    }
                }
                else if (tok[i].IsBuiltin("(") || tok[i].IsBuiltin("["))
                {
                    var func = new CsFunctionCallExpression { Left = left };
                    try { func.Parameters = parseFunctionCall(tok, ref i, out func.IsIndexer); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is List<Tuple<ParameterType, CsExpression>>)
                            func.Parameters = (List<Tuple<ParameterType, CsExpression>>) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, func);
                    }
                    left = func;
                }
                else if (tok[i].IsBuiltin("++"))
                {
                    left = new CsUnaryOperatorExpression { Operand = left, Operator = UnaryOperator.PostfixInc };
                    i++;
                }
                else if (tok[i].IsBuiltin("--"))
                {
                    left = new CsUnaryOperatorExpression { Operand = left, Operator = UnaryOperator.PostfixDec };
                    i++;
                }
            }
            return left;
        }

        private static List<Tuple<ParameterType, CsExpression>> parseFunctionCall(TokenJar tok, ref int i, out bool isIndexer)
        {
            isIndexer = tok[i].IsBuiltin("[");
            if (!tok[i].IsBuiltin("(") && !tok[i].IsBuiltin("["))
                throw new ParseException("'(' or '[' expected.", tok[i].Index);
            i++;
            if (isIndexer && tok[i].IsBuiltin("]"))
                throw new ParseException("Empty indexing expressions are not allowed.", tok[i].Index);
            var parameters = new List<Tuple<ParameterType, CsExpression>>();
            if (!tok[i].IsBuiltin(isIndexer ? "]" : ")"))
            {
                ParameterType pt = ParameterType.In;
                try
                {
                    while (true)
                    {
                        if (tok[i].IsBuiltin("ref"))
                        {
                            pt = ParameterType.Ref;
                            i++;
                        }
                        else if (tok[i].IsBuiltin("out"))
                        {
                            pt = ParameterType.Out;
                            i++;
                        }
                        parameters.Add(Ut.Tuple(pt, parseExpression(tok, ref i)));
                        if (tok[i].IsBuiltin(isIndexer ? "]" : ")"))
                            break;
                        else if (!tok[i].IsBuiltin(","))
                            throw new ParseException(isIndexer ? "',' or ']' expected." : "',' or ')' expected.", tok[i].Index);
                        i++;
                    }
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        parameters.Add(Ut.Tuple(pt, (CsExpression) e.IncompleteResult));
                    throw new ParseException(e.Message, e.Index, parameters);
                }
            }
            i++;
            return parameters;
        }
        private static CsExpression parseExpressionIdentifierOrKeyword(TokenJar tok, ref int i)
        {
            if (tok[i].IsBuiltin("{"))
            {
                try { return new CsArrayLiteralExpression { Expressions = parseArrayLiteral(tok, ref i) }; }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is List<CsExpression>)
                        throw new ParseException(e.Message, e.Index, new CsArrayLiteralExpression { Expressions = (List<CsExpression>) e.IncompleteResult });
                    throw;
                }
            }

            if (tok[i].Type == TokenType.CharacterLiteral)
                return new CsCharacterLiteralExpression { Literal = tok[i++].TokenStr[0] };

            if (tok[i].Type == TokenType.StringLiteral)
                return new CsStringLiteralExpression { Literal = tok[i++].TokenStr };

            if (tok[i].Type == TokenType.NumberLiteral)
                return new CsNumberLiteralExpression { Literal = tok[i++].TokenStr };

            if (tok[i].IsBuiltin("true")) { i++; return new CsBooleanLiteralExpression { Literal = true }; }
            if (tok[i].IsBuiltin("false")) { i++; return new CsBooleanLiteralExpression { Literal = false }; }
            if (tok[i].IsBuiltin("null")) { i++; return new CsNullExpression(); }
            if (tok[i].IsBuiltin("this")) { i++; return new CsThisExpression(); }
            if (tok[i].IsBuiltin("base")) { i++; return new CsBaseExpression(); }

            if (tok[i].IsBuiltin("typeof") || tok[i].IsBuiltin("default"))
            {
                var typof = tok[i].IsBuiltin("typeof");
                var expr = typof ? (CsTypeOperatorExpression) new CsTypeofExpression() : new CsDefaultExpression();
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected after 'typeof' or 'default'.", tok[i].Index);
                i++;
                CsTypeIdentifier ty;
                var tif = typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays;
                try { ty = parseTypeIdentifier(tok, ref i, typof ? tif | typeIdentifierFlags.AllowEmptyGenerics : tif); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsTypeIdentifier)
                        expr.Type = (CsTypeIdentifier) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, expr);
                }
                expr.Type = ty;
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index, expr);
                i++;
                return expr;
            }

            if (tok[i].IsBuiltin("checked") || tok[i].IsBuiltin("unchecked"))
            {
                var expr = tok[i].IsBuiltin("checked") ? (CsCheckedUncheckedExpression) new CsCheckedExpression() : new CsUncheckedExpression();
                i++;
                if (!tok[i].IsBuiltin("("))
                    throw new ParseException("'(' expected after 'checked' or 'unchecked'.", tok[i].Index);
                i++;
                expr.Subexpression = parseExpression(tok, ref i);
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index, expr);
                i++;
                return expr;
            }

            if (tok[i].IsBuiltin("new"))
            {
                i++;
                if (tok[i].IsBuiltin("{"))
                {
                    var anon = new CsNewAnonymousTypeExpression();
                    try { anon.Initializers = parseInitializers(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is List<CsInitializer>)
                            anon.Initializers = (List<CsInitializer>) e.IncompleteResult;
                        throw new ParseException(e.Message, e.Index, anon);
                    }
                    return anon;
                }
                else if (tok[i].IsBuiltin("[") && tok[i + 1].IsBuiltin("]"))
                {
                    i += 2;
                    // Implicitly-typed array
                    if (!tok[i].IsBuiltin("{"))
                        throw new ParseException("'{' expected.", tok[i].Index);
                    try { return new CsNewImplicitlyTypedArrayExpression { Items = parseArrayLiteral(tok, ref i) }; }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is List<CsExpression>)
                            throw new ParseException(e.Message, e.Index, new CsNewImplicitlyTypedArrayExpression { Items = (List<CsExpression>) e.IncompleteResult });
                        throw;
                    }
                }
                else if (tok[i].Type != TokenType.Identifier && tok[i].Type != TokenType.Builtin)
                    throw new ParseException("'{', '[' or type expected.", tok[i].Index);
                else
                {
                    var ty = parseTypeIdentifier(tok, ref i, typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays | typeIdentifierFlags.Lenient);
                    if (tok[i].IsBuiltin("("))
                    {
                        var constructor = new CsNewConstructorExpression { Type = ty };
                        bool dummy;
                        constructor.Parameters = parseFunctionCall(tok, ref i, out dummy);
                        if (tok[i].IsBuiltin("{"))
                            parseConstructorInitializer(constructor, tok, ref i);
                        return constructor;
                    }
                    else if (tok[i].IsBuiltin("["))
                    {
                        // Array construction
                        bool dummy;
                        var k = i;
                        var ret = new CsNewArrayExpression { Type = ty };
                        ret.SizeExpressions.AddRange(parseFunctionCall(tok, ref i, out dummy).Select(p =>
                        {
                            if (p.E1 != ParameterType.In)
                                throw new ParseException("'out' and 'ref' parameters are not allowed in an array constructor.", tok[k].Index);
                            return p.E2;
                        }));
                        while (tok[i].IsBuiltin("["))
                        {
                            i++;
                            int num = 1;
                            while (tok[i].IsBuiltin(","))
                            {
                                num++;
                                i++;
                            }
                            if (!tok[i].IsBuiltin("]"))
                                throw new ParseException("',' or ']' expected.", tok[i].Index, ret);
                            i++;
                            ret.AdditionalRanks.Add(num);
                        }
                        if (tok[i].IsBuiltin("{"))
                        {
                            try { ret.Items = parseArrayLiteral(tok, ref i); }
                            catch (ParseException e)
                            {
                                if (e.IncompleteResult is List<CsExpression>)
                                    ret.Items = (List<CsExpression>) e.IncompleteResult;
                                throw new ParseException(e.Message, e.Index, ret);
                            }
                        }
                        return ret;
                    }
                    else if (tok[i].IsBuiltin("{"))
                    {
                        var constructor = new CsNewConstructorExpression { Type = ty };
                        parseConstructorInitializer(constructor, tok, ref i);
                        return constructor;
                    }
                    else
                        throw new ParseException("'(', '[' or '{' expected.", tok[i].Index);
                }
            }

            if (tok[i].IsBuiltin("delegate"))
            {
                i++;
                var delegateParams = parseParameterList(tok, ref i);
                return new CsAnonymousMethodExpression { Block = parseBlock(tok, ref i), Parameters = delegateParams };
            }

            List<string> parameters = null;
            if (tok[i].Type == TokenType.Identifier && tok[i + 1].IsBuiltin("=>"))
            {
                parameters = new List<string> { tok[i].TokenStr };
                i += 2;
            }
            else if (tok[i].IsBuiltin("("))
            {
                if (tok[i + 1].IsBuiltin(")") && tok[i + 2].IsBuiltin("=>"))
                {
                    parameters = new List<string>();
                    i += 3;
                }
                else if (tok[i + 1].Type == TokenType.Identifier)
                {
                    var ps = new List<string> { tok[i + 1].TokenStr };
                    var j = i + 2;
                    while (tok[j].IsBuiltin(",") && tok[j + 1].Type == TokenType.Identifier)
                    {
                        ps.Add(tok[j + 1].TokenStr);
                        j += 2;
                    }
                    if (tok[j].IsBuiltin(")") && tok[j + 1].IsBuiltin("=>"))
                    {
                        parameters = ps;
                        i = j + 2;
                    }
                }
            }
            if (parameters != null)
            {
                if (tok[i].IsBuiltin("{"))
                {
                    var lambda = new CsBlockLambdaExpression { ParameterNames = parameters };
                    try { lambda.Block = parseBlock(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsBlock)
                        {
                            lambda.Block = (CsBlock) e.IncompleteResult;
                            throw new ParseException(e.Message, e.Index, lambda);
                        }
                        throw;
                    }
                    return lambda;
                }
                else
                {
                    var lambda = new CsSimpleLambdaExpression { ParameterNames = parameters };
                    try { lambda.Expression = parseExpression(tok, ref i); }
                    catch (ParseException e)
                    {
                        if (e.IncompleteResult is CsExpression)
                        {
                            lambda.Expression = (CsExpression) e.IncompleteResult;
                            throw new ParseException(e.Message, e.Index, lambda);
                        }
                        throw;
                    }
                    return lambda;
                }
            }

            if (tok[i].IsBuiltin("("))
            {
                i++;

                // Every time we encounter an open-parenthesis, we don't know in advance whether it's a cast, a sub-expression, or a lambda expression.
                // We've already checked for lambda expression above, so we can rule that one out, but to check whether it's a cast, we need to tentatively 
                // parse it and see if it is followed by ')' and then followed by a unary expression.
                // However, there is an exception:
                //     var blah = (SomeType) +5;
                // The Microsoft C# compiler parses this as a binary expression rather than a cast of a unary expression. Therefore,
                // we specifically check for the four unary operators that are also binary operators and reject the cast so that it becomes a binary expression.

                try
                {
                    var j = i;
                    var ty = parseTypeIdentifier(tok, ref j, typeIdentifierFlags.AllowKeywords | typeIdentifierFlags.AllowSuffixes | typeIdentifierFlags.AllowArrays);
                    if (tok[j].IsBuiltin(")"))
                    {
                        j++;
                        // Don't interpret these as unary operators after a cast. Instead, fail the cast to allow this to be a binary operator expression.
                        if (!tok[j].IsBuiltin("+") && !tok[j].IsBuiltin("-") && !tok[j].IsBuiltin("*") && !tok[j].IsBuiltin("&"))
                        {
                            var operand = parseExpressionUnary(tok, ref j);
                            i = j;
                            return new CsCastExpression { Operand = operand, Type = ty };
                        }
                    }
                }
                catch { }

                // It doesn't look like a valid cast. Try a parenthesised expression.
                var expr = new CsParenthesizedExpression { Subexpression = parseExpression(tok, ref i) };
                if (!tok[i].IsBuiltin(")"))
                    throw new ParseException("')' expected.", tok[i].Index, expr);
                i++;
                return expr;
            }

            return parseExpressionIdentifier(tok, ref i);
        }
        private static void parseConstructorInitializer(CsNewConstructorExpression constructor, TokenJar tok, ref int i)
        {
            tok[i].Assert("{");
            if (tok[i + 1].Type == TokenType.Identifier && tok[i + 2].IsIdentifier("="))
            {
                try { constructor.Initializers = parseInitializers(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is List<CsInitializer>)
                        constructor.Initializers = (List<CsInitializer>) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, constructor);
                }
            }
            else
            {
                try { constructor.Adds = parseArrayLiteral(tok, ref i); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is List<CsExpression>)
                        constructor.Adds = (List<CsExpression>) e.IncompleteResult;
                    throw new ParseException(e.Message, e.Index, constructor);
                }
            }
        }
        private static CsExpression parseExpressionIdentifier(TokenJar tok, ref int i)
        {
            // Check if this can be parsed as a type identifier. If it can't, don't throw; if it failed because of a malformed generic type parameter, it could still be a less-than operator instead.
            try
            {
                var j = i;
                var ty = parseTypeIdentifier(tok, ref j, typeIdentifierFlags.AllowKeywords);
                i = j;
                return new CsTypeIdentifierExpression { Type = ty };
            }
            catch (ParseException) { }

            var ret = new CsTypeIdentifierExpression { Type = new CsConcreteTypeIdentifier { Parts = new List<CsConcreteTypeIdentifierPart> { new CsConcreteTypeIdentifierPart { Name = tok[i].Identifier() } } } };
            i++;
            return ret;
        }

        private static List<CsInitializer> parseInitializers(TokenJar tok, ref int i)
        {
            tok[i].Assert("{");
            i++;
            var list = new List<CsInitializer>();
            while (!tok[i].IsBuiltin("}"))
            {
                if (tok[i].Type != TokenType.Identifier)
                    throw new ParseException("'}' or identifier expected.", tok[i].Index, list);
                var name = tok[i].TokenStr;
                i++;
                if (!tok[i].IsBuiltin("="))
                    throw new ParseException("'=' expected. (2)", tok[i].Index, list);
                i++;
                try { list.Add(new CsInitializer { Name = name, Expression = parseExpression(tok, ref i) }); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        list.Add(new CsInitializer { Name = name, Expression = (CsExpression) e.IncompleteResult });
                    throw new ParseException(e.Message, e.Index, list);
                }
                if (!tok[i].IsBuiltin("}") && !tok[i].IsBuiltin(","))
                    throw new ParseException("'}' or ',' expected.", tok[i].Index, list);
                if (tok[i].IsBuiltin("}"))
                    break;
                i++;
            }
            i++;
            return list;
        }
        private static List<CsExpression> parseArrayLiteral(TokenJar tok, ref int i)
        {
            tok[i].Assert("{");
            i++;
            var list = new List<CsExpression>();
            while (!tok[i].IsBuiltin("}"))
            {
                try { list.Add(parseExpression(tok, ref i)); }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsExpression)
                        list.Add((CsExpression) e.IncompleteResult);
                    throw new ParseException(e.Message, e.Index, list);
                }
                if (!tok[i].IsBuiltin("}") && !tok[i].IsBuiltin(","))
                    throw new ParseException("'}' or ',' expected.", tok[i].Index, list);
                if (tok[i].IsBuiltin("}"))
                    break;
                i++;
            }
            i++;
            return list;
        }
    }

    public class ParseException : Exception
    {
        private int _index;
        private object _incompleteResult;
        public ParseException(string message, int index) : this(message, index, null, null) { }
        public ParseException(string message, int index, object incompleteResult) : this(message, index, incompleteResult, null) { }
        public ParseException(string message, int index, object incompleteResult, Exception inner) : base(message, inner) { _index = index; _incompleteResult = incompleteResult; }
        public int Index { get { return _index; } }
        public object IncompleteResult { get { return _incompleteResult; } }
    }
}
