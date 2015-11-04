using System.Linq.Expressions;

namespace RT.ParseCs
{
    /// <summary>
    ///     <para>
    ///         The main class to look at is <see cref="Parser"/>. This gives you the ability to parse a C# file, a statement
    ///         or a expression into a parse-tree representation. That parse tree does not do any compiling; names (of types,
    ///         variables etc.) are still strings.</para>
    ///     <para>
    ///         You can further convert a parse tree of an expression (<see cref="CsExpression"/>) into the expression classes
    ///         in the <c>System.Linq.Expressions</c> namespace. At this point, ParseCs will do name resolution, method
    ///         overload resolution, type inference, etc. to convert all the names into actual references.</para>
    ///     <para>
    ///         If the expression you converted in this way is a lambda expression (<see cref="CsLambdaExpression"/>), you get
    ///         back a <see cref="LambdaExpression"/> that you can compile into executable code by using <c>Compile()</c> or
    ///         <c>CompileToMethod()</c>.</para></summary>
    sealed class NamespaceDocumentation { }
}
