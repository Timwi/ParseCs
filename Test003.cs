using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace RT.ParseCs.Tests
{
    [TestFixture]
    class Test003
    {
        [Test]
        public void TestEverything()
        {
            var source = @"
using System;
using Blah = System.Type;
namespace XYZ
{
    public abstract class Abstract : Base
    {
        [MethodCustomAttribute]
        public void Method() { }
        public abstract int MethodInt();
        private List<int> GetInts() { return new List<int> { unchecked(1 + 1*2), checked(a & b | c ^ d), int.MaxValue }; }
        static extern void MessageBox(string message);
    }
    internal static class Static<T1, [Foo] T2, T3, T4> where T1 : new() where T2 : class where T3 : struct where T4 : System.IComparable<List<Abstract>>
    {
        [FieldCustomAttribute]
        public static volatile int Field;
        public const int Constant = 5;
        public readonly int ReadOnly = 5;
        [EventCustomAttribute]
        public event EventHandler MyEvent = null, OtherEvent = delegate { };
        public event EventHandler YetOtherEvent
        {
            [MethodCustomAttribute]
            add { }
            remove { }
        }
        private Something Private = initialize();
        internal Something Internal = initialize();
        ReturnType method(this Type extensionParam, ref Type refParam, out Type outParam, params object[] array) { }
        public new NewMethod() { }
        public override sealed Nothing NewMethod() { }
    }

    [CustomAttribute1, CustomAttribute2]
    struct MyStruct { }
    [CustomAttribute2(null)]
    enum MyEnum { A, [ValueAttribute] B, C = 1 << 3 }
    interface MyInterface { void Method(); }
    interface MyGenericInterface<in X, out Y> { void Method(); }
    delegate System.A<X>.B<Y> MyDelegate(int integer, MyStruct @struct, [ParameterCustomAttribute] System.Type type = typeof(IEnumerable<>), DateTime? stamp = null, bool[] arr = null);
    delegate void MyGenericDelegate<in X, out Y>();
    sealed partial class MoreCrazyCases
    {
        public partial void PartialMethod();
        public partial void PartialMethodImpl() { }
        public unsafe Pointer Unsafe() { }
        public int Property { get; set; }
        public int Property { get; private set; }
        public abstract int Property { get; set; }
        public int Property { get { } set { } }
        public int Property { get { } private set { } }

        int Blah();
        private int Blah();
        internal int Blah();
        protected int Blah();
        protected internal int Blah();
        public int Blah();

        public class NestedType { }
    }

    class ExplicitImplementations : global::System.IInterface<string>.IAnother<int>
    {
        IEnumerator<char> System.IInterface<string>.IAnother<int>.GetEnumerator()
        {
            yield return '\'';
            yield break;
            throw new NotImplementedException(positionalArgument, out outArgument, ref refArgument, name: namedArgument, nameOut: out namedOutArgument, nameRef: ref namedRefArgument);
        }
        event EventHandler System.IInterface<string>.IAnother<int>.MyEvent { add { } remove { } }
        SomethingOrOther System.IInterface<string>.IAnother<int>.MyGetOnlyProperty { get { } }
        SomethingOrOther System.IInterface<string>.IAnother<int>.this[string blah, int index] { get { } set { } }
        SomethingOrOther System.IInterface<string>.IAnother<int>.MySetOnlyProperty { set { } }
    }

    class ConstrDestr
    {
        public ConstrDestr() : this(null) { }
        private ConstrDestr(Type type) : base(type) { }
        private ConstrDestr(int one, string two) { }
        ~ConstrDestr() { SelfDestruct(); }
        static ConstrDestr() { Console.WriteLine(null); }
    }

    class OperatorOverloads
    {
        public static bool operator ~(X x) { }
        public static bool operator !(X x) { }
        public static bool operator %(X x, Y y) { }
        public static bool operator ^(X x, Y y) { }
        public static bool operator &(X x, Y y) { }
        public static bool operator *(X x, Y y) { }
        public static bool operator -(X x) { }
        public static bool operator -(X x, Y y) { }
        public static bool operator +(X x) { }
        public static bool operator +(X x, Y y) { }
        public static bool operator --(X x) { }
        public static bool operator ++(X x) { }
        public static bool operator |(X x, Y y) { }
        public static bool operator <(X x, Y y) { }
        public static bool operator <<(X x, Y y) { }
        public static bool operator <=(X x, Y y) { }
        public static bool operator >(X x, Y y) { }
        public static bool operator >>(X x, Y y) { }
        public static bool operator >=(X x, Y y) { }
        public static bool operator ==(X x, Y y) { }
        public static bool operator !=(X x, Y y) { }
        public static bool operator true(Y y) { }
        public static bool operator false(Y y) { }
        public static implicit operator SomeType(X x) { }
        public static explicit operator SomeType(X x) { }
    }

    class StatementsAndExpressions
    {
        public void Method()
        {
            bool b = true;
            char c = ' ';
            byte b;
            sbyte sb;
            short s;
            ushort us;
            int i;
            uint ui = 0U;
            long l = 0L;
            ulong ul = 0UL;
            double d = 0d;
            float f = 0f;
            string s = ""Blah \n \a \b \v \t \r \0 \f \\ \"""" + @""Blah \a """" \b"";
            decimal d = 0m;
            object o = null;

            var expressions = { true, false, null, this, typeof(Something), default(Something), sizeof(Something), checked(Something), unchecked(Something) };

            var operators = {
                ~bitwiseNot,
                !logicalNot,
                equality != equality,
                mod1 % mod2,
                mod1 %= mod2,
                x ^ or,
                x ^= or,
                &address,
                a & nd,
                a &= nd,
                a && nd,
                *pointer,
                multi * ply,
                multi *= ply,
                ca(ll),
                pointer->deref,
                mi - nus,
                mi -= nus,
                a + dd,
                a += dd,
                --decrement,
                decrement--,
                ++increment,
                increment++,
                assign = ment,
                e == quality,
                index[ing],
                or | or,
                or |= or,
                or || or,
                less < than,
                lessthan <= orequal,
                shift << left,
                shift <<= left,
                greater > than,
                greaterthan >= orequal,
                shift >> right,
                shift >>= right,
                con ? di : tion,
                coa ?? lesce,
                mem.ber,
                di / vide,
                di /= vide,

                one is other,
                one as other
            };

            var b = base.Method();
            break;
            checked
            {
                char c = checked(int.MaxValue + 1);
                object[] ĉ = { (Cast) xyz, (Cast) ~1, (Cast) !x, (Cast) * 2, (int) +1, (int) -1, (int) &y, (NotACast) + 1, (NotACast) - 1, (NotACast) & y };
                continue;
            }
            const int x = 5;
            var @delegate = delegate { };
            var @delegate = delegate() { };
            var @delegate = delegate(int x, out string y, ref List z) { };
            do
            {
                fixed (byte* b = Blah()) { }
            }
            while (true);
            for (int i = 0; i < 10; i++) { }
            for (i = 0; i < 10; i++) { }
            for (int i = 0, j = 0; i < 10; i++, j++) { }
            for (i = 0, j = 0; i < 10; i++, j++) { }
            foreach (var item in collection) { }
            goto label;
            goto case 1;
            goto default;
            if (true) { }
            if (true) { } else { }
            label: ;
            lock (blah) { }
            new Class();
            new Class { A = a };
            new Class { a, b, c };
            var obj = new { A = a, B = b };
            var arr = new[] { a, b, c };
            var arr = new int[] { 0, 1, 2 };
            var arr = new int[,] { { 0, 1, 2 }, { 0, 1, 2 } };
            int[] arr = new int[5];
            int[][] arr = new int[5][];
            int[][,] arr = new int[5][,];
            int[,][] arr = new int[1, 2][];
            return;
            return xyz;
            switch (variable)
            {
                case 1:
                    break;
                default:
                    break;
            }
            byte[] b = stackalloc byte[5], c = stackalloc byte[6];
            throw;
            throw new Exception();
            try { } catch { }
            try { } catch (Exception) { }
            try { } catch (Exception e) { } finally { }
            try { } finally { }
            using (var x = blah) { }
            using (x = blah) { }
            unsafe
            {
                unchecked
                {
                    var u = unchecked(int.MaxValue + v);
                }
            }
            void* v;
            while (true) { }
            yield break;
            yield return 1;

            var q = from i in collection
                join k in collection2 on k equals j into n
                let j = i % 5
                where j == 0
                orderby j ascending, i descending, k
                select i;
            var q = from i in collection
                group gr by i % 5;
            var q = from i in collection
                group gr by i % 5 into g
                select gr;
            var q = from i in collection
                join k in collection2 on k equals j
                select k;

            var lamba = () => blah;
            var lamba = x => blah;
            var lamba = (x, y) => blah;
            var lamba = (int x, ref List y) => blah;
            var lamba = () => { blah(); };
            var lamba = x => { blah(); };
            var lamba = (int x, out List y) => { blah(); };
        }
    }
}
";

            var document = Parser.ParseDocument(source);
            var after = document.ToString();

            Assert.AreEqual(Regex.Replace(source, @"\s", ""), Regex.Replace(after, @"\s", ""));
        }
    }
}
