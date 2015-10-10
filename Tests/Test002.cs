using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace RT.ParseCs.Tests
{
    [TestFixture]
    class Test002
    {
        [Test]
        public void TestAutoList()
        {
            var auto = new AutoList<string>();
            Assert.IsNotNull(auto);
            Assert.AreEqual(0, auto.Count);
            Assert.IsNull(auto[1]);
            Assert.AreEqual(0, auto.Count);
            auto[5] = "five";
            for (int i = 0; i < 5; i++)
                Assert.IsNull(auto[i]);
            Assert.IsNotNull(auto[5]);
            Assert.AreEqual("five", auto[5]);
        }

        [Test]
        public void TestAutoList2()
        {
            var auto = new AutoList<string>(5);
            Assert.AreEqual(0, auto.Count);
            Assert.IsNull(auto[1]);
            Assert.AreEqual(0, auto.Count);
        }

        [Test]
        public void TestAutoListInitialize()
        {
            var auto = new AutoList<string>(new[] { "a", "b", "c" });
            Assert.AreEqual(3, auto.Count);
            Assert.AreEqual("c", auto[2]);
        }
    }
}
