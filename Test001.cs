using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace RT.ParseCs.Tests
{
    [TestFixture]
    class Test001
    {
        [Test]
        public void TestBlankDocument()
        {
            Assert.Throws<ArgumentNullException>(() => { Parser.ParseDocument(null); });

            var parsed = Parser.ParseDocument("");
            Assert.NotNull(parsed.CustomAttributes);
            Assert.IsEmpty(parsed.CustomAttributes);
            Assert.AreEqual(0, parsed.EndIndex);
            Assert.AreEqual(0, parsed.StartIndex);
            Assert.NotNull(parsed.Namespaces);
            Assert.IsEmpty(parsed.Namespaces);
            Assert.NotNull(parsed.Types);
            Assert.IsEmpty(parsed.Types);
            Assert.NotNull(parsed.UsingAliases);
            Assert.IsEmpty(parsed.UsingAliases);
            Assert.NotNull(parsed.UsingNamespaces);
            Assert.IsEmpty(parsed.UsingNamespaces);
        }
    }
}
