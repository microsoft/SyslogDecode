// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.SampleApp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using SyslogDecode.Common;
    using SyslogDecode.Udp;

    class Program
    {
        static int TotalCount = 100 * 1000;
        static SyslogUdpSender _sender;
        static SyslogUdpPipeline _pipeline; 
        static HashSet<string> _detectedIpAddresses = new HashSet<string>();
        private static object _lock = new object();

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Syslog Sample App started.");
                Console.WriteLine($"Sending {TotalCount} syslog messages to local port 514");
                // Create sender and listener
                _sender = new SyslogUdpSender("127.0.0.1", 514);
                _pipeline = new SyslogUdpPipeline(IPAddress.Parse("127.0.0.1"));
                _pipeline.Error += Pipeline_Error;
                _pipeline.StreamParser.ItemProcessed += StreamParser_ItemProcessed;
                _pipeline.Start(); 
                // Send syslog entries of different kinds
                int sentCount = 0; 
                foreach(var message in SyslogMessageGenerator.CreateTestSyslogStream(TotalCount))
                {
                    _sender.Send(message);
                    sentCount++;
                }
                Console.WriteLine("Sent, waiting for receive completion...");
                _pipeline.Stop();
                Console.WriteLine($"Done. Sent: {sentCount}, received: {_pipeline.UdpListener.PacketCount}");
                // Verify that all IP addresses are detected
                var allDetected = SyslogMessageGenerator.AllIpAddresses.All(ip => _detectedIpAddresses.Contains(ip)); 
                Console.WriteLine($"      Ip addresses expected: 6, detected: {_detectedIpAddresses.Count},  all detected: {allDetected}");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                Console.WriteLine($"Done.");
                Console.WriteLine($"Press any key...");
                Console.ReadKey();
            }

        }

        private static void Pipeline_Error(object sender, ErrorEventArgs e)
        {
            Console.WriteLine(e.Error.ToString());
        }

        // Note that parsing of syslog messages inside SyslogListener happens on multiple background threads, 
        //  so EntryReceived is fired concurrently. So we need to be careful with concurrency here.
        private static void StreamParser_ItemProcessed(object sender, ItemEventArgs<Model.ParsedSyslogMessage> e)
        {
            var msg = e.Item;
            var ipAddresses = msg.ExtractedTuples.Where(nv => nv.Name == "IPv4" || nv.Name == "IPv6").Select(nv => nv.Value).ToList();
            lock (_lock) // beware of concurrency
            {
                _detectedIpAddresses.UnionWith(ipAddresses);
            }
        }

    }
}
