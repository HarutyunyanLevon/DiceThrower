using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class JsonTests
    {
        [Test]
        public void RoundTripKeepsValuesAndStructure()
        {
            var original = new Dictionary<string, object>
            {
                { "text", "a \"quoted\" back\\slash\nnew line\ttab \u0001 and unicode é" },
                { "yes", true },
                { "no", false },
                { "nothing", null },
                { "int", 42 },
                { "negative", -7L },
                { "real", 1.5 },
                { "list", new List<object> { 1, "two", new List<object>(), new Dictionary<string, object>() } },
            };

            var back = (Dictionary<string, object>)Json.Parse(Json.Write(original));

            Assert.AreEqual(original["text"], back["text"]);
            Assert.AreEqual(true, back["yes"]);
            Assert.AreEqual(false, back["no"]);
            Assert.IsNull(back["nothing"]);
            Assert.AreEqual(42L, back["int"]);
            Assert.AreEqual(-7L, back["negative"]);
            Assert.AreEqual(1.5, back["real"]);
            var list = (List<object>)back["list"];
            Assert.AreEqual(4, list.Count);
            Assert.AreEqual("two", list[1]);
        }

        [Test]
        public void ParsesWhitespaceAndEscapes()
        {
            var v = (Dictionary<string, object>)Json.Parse(" { \"a\" : [ 1 , 2 ] , \"b\" : \"\\u0041\\n\" } ");
            Assert.AreEqual(2, ((List<object>)v["a"]).Count);
            Assert.AreEqual("A\n", v["b"]);
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("[1,")]
        [TestCase("[1 2]")]
        [TestCase("{\"a\" 1}")]
        [TestCase("\"unterminated")]
        [TestCase("nope")]
        [TestCase("{} extra")]
        [TestCase("1.2.3")]
        public void MalformedTextThrowsFormatException(string text)
        {
            Assert.Throws<FormatException>(() => Json.Parse(text));
        }

        [Test]
        public void UnsupportedValueIsRejectedWhenWriting()
        {
            Assert.Throws<ArgumentException>(() => Json.Write(new object()));
        }
    }
}
