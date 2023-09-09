﻿using System;
using Linguini.Syntax.Parser;
using NUnit.Framework;

namespace Linguini.Syntax.Tests.Parser
{
    [TestFixture]
    public class LinguiniTestDetailedErrors
    {
        [Test]
        [TestCase("### Comment\nterm1", 2, 12, 17, 17, 18)]
        public void TestDetailedErrors(string input, int row, int startErr, int endErr,
            int startMark, int endMark)
        {
            var parse = new LinguiniParser(input).Parse();
            var parseWithComments = new LinguiniParser(input).ParseWithComments();

            Assert.AreEqual(1, parse.Errors.Count);
            Assert.AreEqual(1, parseWithComments.Errors.Count);

            var detailMsg = parse.Errors[0];
            Assert.AreEqual(row, detailMsg.Row);
            Assert.IsNotNull(detailMsg.Slice);
            Assert.AreEqual(new Range(startErr, endErr), detailMsg.Slice!.Value);
            Assert.AreEqual(new Range(startMark, endMark), detailMsg.Position);
        }

        [Test]
        public void TestLineOffset()
        {
            const string code = @"a = b
c = d

foo = {

d = e
";

            var parser = new LinguiniParser(code);
            var result = parser.Parse();
            var error = result.Errors[0];

            Assert.That(error.Row, Is.EqualTo(6));
        }
    }
}
