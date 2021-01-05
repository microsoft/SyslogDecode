// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/


namespace Microsoft.Syslog.Tests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Syslog.Parsing;
    using System.Text;
    using Microsoft.Syslog.Model;

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

            // Parser failure - RFC-5424 message without structured element
            // the following message resulted in parsing error reported in prod:  Expected [ for structured data. (near 94)
            //   The message in RFC-5424 format is missing structured data element, and instead lists some values in the 'Message' section
            //    parser expected '[' at the beginning of the fragment '0|Microsoft|Azure' on the following line
            msg = @"<116>1 2020-06-23T16:27:38.172630+00:00 CO2PHXDC25 CEF 29060 SensorDisconnectedMonitoringAler ﻿0|Microsoft|Azure
ATP|2.117.8234.36397|SensorDisconnectedMonitoringAlert|SensorDisconnectedMonitoringAlert|5|externalId=1011 cs1Label=url cs1=https://cedisatpgmews1.atp.azure.com/monitoring
cs2Label=trigger cs2=new msg=There has not been communication from the Sensor DB3GMEVDC02 for 1 week. Last communication was on 6/23/2020 4:17:11 PM UTC."
.Replace(Environment.NewLine, " ");
            rawMsg = new RawSyslogMessage() { Message = msg };
            parsedMsg = parser.Parse(rawMsg);
            Assert.IsTrue(parsedMsg.ErrorMessages.Count == 0, "Failed to parse RFC-5424 message without structured data element");

            // parser failure: failed to detect IP address when it is followed by port
            // Cause:  key-value list parser did not parse properly the uploaderAddress value
            msg = @"<30> DefsServer='azweccol03' App='DefsLite' Version='1.0.8d' UploaderAddress='10.236.29.53:61190' 
Created zipName='D:\OSSCWEC\EventLogs\Archive-OSSCWEC-Retention12-DM2CI1WECCONW02-2020-06-30-10-02-53-471.evtx-zip.Compressed'"
.Replace(Environment.NewLine, " ").Replace("'", "\"");
            rawMsg = new RawSyslogMessage() { Message = msg };
            parsedMsg = parser.Parse(rawMsg);
            Assert.IsTrue(parsedMsg.ErrorMessages.Count == 0, "Failed to detect IP address ");
            parsedMsg.Data.TryGetValue("IPv4", out object ipList);
            Assert.IsNotNull(ipList, "Ip is not detected.");
        }

    }
}
