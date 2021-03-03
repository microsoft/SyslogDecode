// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogDecode.Common;
    using SyslogDecode.Model;
    using SyslogDecode.Parsing;
    using SyslogDecode.Udp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SyslogLoadTest
    {

        /*
         This is a load test for SyslogListener. The code creates listener and then sends N messages to UDP port 514, using m sending theads
         The listener receives, parses messages and reports them through event. The event handler counts the received messages.
         */
        [TestMethod]
        public void TestSendReceive()
        {
            var itemsPerThread = 100 * 1000;
            var senderThreadCount = 2;
            
            int totalReceived = 0;
            int totalErrors = 0; 
            int totalParseErrors = 0;
            int maxQueueLength = 0;
            long maxUdpBufferSize = 0;
            

            // Pipeline 
            var parser = SyslogMessageParser.CreateDefault();
            var localAddr = IPAddress.Parse("127.0.0.1");
            var pipeline = new SyslogUdpPipeline(localAddr);
            var outListener = new SyslogOutListener(pipeline);
            
            pipeline.StreamParser.Subscribe(outListener); 
            
            
            pipeline.Error += (s, e) =>
            {
                totalErrors++;
                Trace.WriteLine(e.ToString());
            };
            pipeline.StreamParser.ItemProcessed += (s, e) =>
            {
                Interlocked.Increment(ref totalReceived);
                var errMessages = e.Item.ErrorMessages;
                if (errMessages.Count > 0)
                {
                    Interlocked.Add(ref totalParseErrors, errMessages.Count);
                }

                var queueLen = pipeline.StreamParser.BufferQueueCount;
                if (queueLen > maxQueueLength)
                    maxQueueLength = queueLen;
                var bufSize = pipeline.UdpListener.GetUdpBufferBytesAvailable();
                if (bufSize > maxUdpBufferSize)
                    maxUdpBufferSize = bufSize;
            };
            pipeline.Start();

            // start n parallel sending tasks
            var tasks = new List<Task>(); 
            for (int i = 0; i < senderThreadCount; i++)
                tasks.Add(Task.Run(() => SendSyslogBatch(i, itemsPerThread)));
            Task.WaitAll(tasks.ToArray()); 

            // wait until all processed
            Thread.Sleep(10); //let messages arrive at UDP listener
            pipeline.Stop(); 

            Trace.WriteLine($"MaxQueueLength: {outListener.MaxQueueLength}, MaxUdpBufferSize = {outListener.MaxUdpBufferSize /1024} Kb ");

            // verify
            Assert.AreEqual(0, outListener.TotalErrors, "Encountered listener errors");
            Assert.AreEqual(0, outListener.TotalParseErrors, "Encountered parse errors");
            var sentCount = itemsPerThread * senderThreadCount;
            Assert.AreEqual(sentCount, outListener.TotalReceived, "Lost some messages");
        }

        private static void SendSyslogBatch(int procNum, int itemCount)
        {
            var client = new SyslogUdpSender("127.0.0.1");
            for (int i = 0; i < itemCount; i++)
            {
                var entry = new ParsedSyslogMessage(Facility.Authorization, Severity.Alert, DateTime.UtcNow, "Local", "TestApp", "ProcId" + procNum, "Msg" + i, 
                               "Something happened here at " + DateTime.Now);
                var prmList = new List<NameValuePair>();
                var elemName = "MainElem";
                prmList.Add(new NameValuePair() { Name = "Prm1", Value = "Val1" });
                entry.StructuredData5424[elemName] = prmList;
                client.Send(entry);
                // Yield occasionally to let receiver threads to run
                if (i % 100 == 0) Thread.Yield();
            }
        }

        class SyslogOutListener : IObserver<ParsedSyslogMessage>
        {
            SyslogUdpPipeline _pipeline; 
            public int TotalReceived = 0;
            public int TotalErrors = 0;
            public int TotalParseErrors = 0;
            public int MaxQueueLength = 0;
            public long MaxUdpBufferSize = 0;
            
            public SyslogOutListener(SyslogUdpPipeline pipeline)
            {
                _pipeline = pipeline; 
            }

            public void OnNext(ParsedSyslogMessage msg)
            {
                Interlocked.Increment(ref TotalReceived);
                var errMessages = msg.ErrorMessages;
                if (errMessages.Count > 0)
                {
                    Interlocked.Add(ref TotalParseErrors, errMessages.Count);
                }

                var queueLen = _pipeline.StreamParser.BufferQueueCount;
                if (queueLen > MaxQueueLength)
                    MaxQueueLength = queueLen;
                var bufSize = _pipeline.UdpListener.GetUdpBufferBytesAvailable();
                if (bufSize > MaxUdpBufferSize)
                    MaxUdpBufferSize = bufSize;
            }
            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
                TotalErrors++;
                Trace.WriteLine(error.ToString());
            }
        }

    }
}
