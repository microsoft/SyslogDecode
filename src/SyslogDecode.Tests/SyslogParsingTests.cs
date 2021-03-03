// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/


namespace SyslogDecode.Tests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SyslogDecode.Parsing;
    using System.Text;
    using SyslogDecode.Model;

    [TestClass]
    public class SyslogParsingTests
    {
        [TestMethod]
        public void TestParserAndSerializer()
        {
            // A sample from RFC 5424
            var header = "<34>1 2003-10-11T22:14:15.003Z mymachine.example.com su - ID47";
            var structData = "[exampleSDID@32473 iut=\"3\" eventSource=\"Application\" eventID=\"1011\"]";
            var customMsg = "My custom message";

            var parser = SyslogMessageParser.CreateDefault();

            // Test 1 - simple message
            var text1 = header + " " + structData + " " + customMsg;
            var rawMsg = new RawSyslogMessage() { Message = text1 };
            var parsedMsg = parser.Parse(rawMsg);
            Assert.IsNotNull(parsedMsg);
            Assert.AreEqual(0, parsedMsg.ErrorMessages.Count);
            Assert.AreEqual(1, parsedMsg.StructuredData5424.Count, "expected 1 element in structured data");
            var elem1Name = parsedMsg.StructuredData5424.First().Key;
            var elem1Params = parsedMsg.StructuredData5424.First().Value; 
            Assert.AreEqual(3, elem1Params.Count, "Expected 3 parameter");
            var prmEventId = elem1Params.First(p => p.Name == "eventID");
            Assert.AreEqual("1011", prmEventId.Value, "Invalid param value.");

            // Test 2 - escaped chars in param values;  the test value is   \abc\def"ghi 
            // We escape it appropriately is entry text, and parser should 'unescape' it. We use interpolated string to produce string with escapes
            var dq = '"';
            var esc = '\\';
            var structDataWithEscapedChars = $"[exEscapes@123 escParam=\"{esc}{esc}abc{esc}{esc}def{esc}{dq}ghi\"]";

            var text2 = header + " " + structDataWithEscapedChars + " " + customMsg;
            rawMsg = new RawSyslogMessage() { Message = text2 };
            parsedMsg = parser.Parse(rawMsg);
            Assert.IsNotNull(parsedMsg);
            Assert.AreEqual(0, parsedMsg.ErrorMessages.Count);
            var escParam = parsedMsg.StructuredData5424.First().Value[0];
            Assert.AreEqual(@"\abc\def""ghi", escParam.Value, "Invalid value of parameter with escapes.");

            // Test 3: test serializer: serialize entry and compare to original message text
            // Note  that with real messages there maybe mismatches because of slightly different dates formatting by serializer (ex: number of ms in datetime)
            var back2 = SyslogSerializer.Serialize(parsedMsg);
            Assert.AreEqual(text2, back2, "Serialized entry does not match.");

            // Test 4 - using Message field with BOM prefix
            // The parser should strip the BOM
            var msgWithBOM = PrependBOM(customMsg);
            var text3 = header + " " + structDataWithEscapedChars + " " + msgWithBOM;
            rawMsg = new RawSyslogMessage() { Message = text3 };
            parsedMsg = parser.Parse(rawMsg);
            Assert.IsNotNull(parsedMsg);
            Assert.AreEqual(customMsg, parsedMsg.Message, "Message does not match.");

        }

        // Prepends Byte-order-mark sequence, allowed by RFC-5424, to test how it is handled by the parser
        private string PrependBOM(string value)
        {
            var utf8 = Encoding.UTF8; 
            var bytes = utf8.GetBytes(value);
            var bom = utf8.GetPreamble(); //this is BOM
            var allBytes = bom.Concat(bytes).ToArray();
            var result = utf8.GetString(allBytes);
            return result; 
        }

        [TestMethod]
        public void TestBugFixes()
        {
            var parser = SyslogMessageParser.CreateDefault();
            string msg;
            RawSyslogMessage rawMsg;
            ParsedSyslogMessage parsedMsg; 
            bool success; 


            // Parser failure 1
            msg = "<28>Jun 30 14:29:50 Sg2-0102-0201-14T1 snmp#snmp-subagent"; // RFC-3164: no Message element
            rawMsg = new RawSyslogMessage() { Message = msg };
            parsedMsg = parser.Parse(rawMsg);
            Assert.IsTrue(parsedMsg.ErrorMessages.Count == 0, "Failed to parse RFC-3164 message without 'Message' element");

            // Parser failure 2
            msg = "<28>Jun 30 14:29:50 Sg2-0102-0201-14T1 "; //  RFC-3164: no procId
            rawMsg = new RawSyslogMessage() { Message = msg };
            parsedMsg = parser.Parse(rawMsg);
            Assert.IsTrue(parsedMsg.ErrorMessages.Count == 0, "Failed to parse RFC-3164 message without ProcId element");

        }

    }
}
