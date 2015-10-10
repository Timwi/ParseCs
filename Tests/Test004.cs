using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace RT.ParseCs.Tests
{
    [TestFixture]
    class Test004
    {
        [Test]
        public void TestCsTypeNameGetSingleIdentifier()
        {
            var doc = Parser.ParseDocument("class X { void IInterface.M<T>() { } }");

            assertThrowsMessage<ParseException>(() => Parser.ParseDocument("class X { void IInterface.M<T?>() { } }"), "Invalid generic type parameter declaration.");
            assertThrowsMessage<ParseException>(() => Parser.ParseDocument("class X { void IInterface.M<T*>() { } }"), "Invalid generic type parameter declaration.");
            assertThrowsMessage<ParseException>(() => Parser.ParseDocument("class X { void IInterface.M<T[]>() { } }"), "Invalid generic type parameter declaration.");
            assertThrowsMessage<ParseException>(() => Parser.ParseDocument("class X { void IInterface.M<T<T>>() { } }"), "Invalid generic type parameter declaration.");
        }

        private void assertThrowsMessage<TException>(TestDelegate code, string message) where TException : Exception
        {
            try
            {
                code();
                Assert.Fail("Expected exception {0} wasn’t thrown.", typeof(TException).FullName);
            }
            catch (TException e)
            {
                Assert.AreEqual(message, e.Message);
            }
        }
    }
}
