// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace Microsoft.Syslog.TestLoadApp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tx.Network;
    using Tx.Network.Snmp;

    using Microsoft.Syslog;
    using Microsoft.Syslog.Parsing;
    using Microsoft.Syslog.Udp;

    public static class PCapFileProcessor
    {

        public static Task SendFile(string pcapFile, int repeatCount, int maxEps, int maxRecordsToSend, string ipAddress = "127.0.0.1", int port = 514)
        {
            if (!File.Exists(pcapFile))
            {
                Console.WriteLine($"File '{pcapFile}' does not exist. ");
                return Task.CompletedTask;
            }

            return Task.Run(() => DoSendFile(pcapFile, repeatCount, maxEps, maxRecordsToSend, ipAddress, port));
        }

        private static void DoSendFile(string pcapFile, int repeatCount, int maxEps, int maxRecordsToSend, string ipAddress = "127.0.0.1", int port = 514)
        {
            try
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    Console.WriteLine($"==== Sending file '{pcapFile}', iteration {i + 1} of {repeatCount}. ====");
                    DoSendFileOnce(pcapFile, maxEps, ipAddress, port, maxRecordsToSend);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("!!!!    Exception: " + ex.ToString());
            }

        }

        private static void DoSendFileOnce(string pcapFile, int maxEps, string ipAddress, int port, int maxRecordsToSend)
        {
            int recCount = 0;
            int sysLogMessageCount = 0;

            var sender = new SyslogUdpSender(ipAddress, port);

            var packets = PcapNg.ReadForward(pcapFile)
                .Where(b => b.Type == BlockType.EnhancedPacketBlock)
                .Cast<EnhancedPacketBlock>()
                .ParseUdp();

            var start = DateTime.UtcNow; 
            // Main loop - read pcapFile, parse entries, apply regex extractor, create result record, send it to Kusto
            foreach (var udpPacket in packets)
            {
                recCount++;
                if (recCount % 50000 == 0)
                {
                    Console.WriteLine($@"  Loaded {recCount} records, sent syslog entries: {sysLogMessageCount}");
                }
                var bytes = udpPacket.Data.ToArray();
                var syslogString = Encoding.UTF8.GetString(bytes);
                var isSyslog = syslogString.StartsWith(SyslogChars.SyslogStart) || syslogString.StartsWith(SyslogChars.SyslogStartBom);
                if (!isSyslog)
                {
                    continue;
                }
                sysLogMessageCount++;
                sender.Send(syslogString);
                CheckNeedSlowDown(start, sysLogMessageCount, maxEps);

                if (maxRecordsToSend < recCount)
                    break;

            }//foreach

            var timeSec = DateTime.UtcNow.Subtract(start).TotalSeconds;

            var eps = (timeSec > 0) ? (int)(sysLogMessageCount / timeSec) : 1;
            Console.WriteLine();
            Console.WriteLine($@"
========= FINAL TOTALS:  loaded {recCount} records, sent syslog entries: {sysLogMessageCount}, time: {timeSec}, EPS: {eps} ===============");
        }// method SendFile


        private static void CheckNeedSlowDown(DateTime start, int recCount, int maxEps)
        {
            if (recCount % 10 != 0) // check only every 10th record
                return;
            var timeSec = DateTime.UtcNow.Subtract(start).TotalSeconds;
            if (timeSec == 0)
                return; 
            var eps = recCount / timeSec;
            if (eps > maxEps)
                Thread.Sleep(20); //sleep for 20 ms

        }

        // Use it to export pcap file as text
        public static void ExportPcapFile(string pcapFile, string outFile)
        {
            if (File.Exists(outFile))
                File.Delete(outFile);

            if (!File.Exists(pcapFile))
            {
                Console.WriteLine($"File '{pcapFile}' does not exist. ");
                return;
            }
            Console.WriteLine($"Reading syslog messages from file {pcapFile} ...");

            // open the file
            var packets = PcapNg.ReadForward(pcapFile)
                .Where(b => b.Type == BlockType.EnhancedPacketBlock)
                .Cast<EnhancedPacketBlock>()
                .ParseUdp();

            var messages = new List<string>();
            int totalCount = 0;
            foreach (var udpPacket in packets)
            {
                var bytes = udpPacket.Data.ToArray();
                var syslogString = Encoding.UTF8.GetString(bytes);
                if (!syslogString.StartsWith("<"))
                    continue;
                messages.Add(syslogString);
                totalCount++;
                if (messages.Count > 500)
                {
                    File.AppendAllLines(outFile, messages);
                    messages.Clear();
                    Console.WriteLine($"Parsed {totalCount} messages");
                }
            }
            if (messages.Count > 0)
                File.AppendAllLines(outFile, messages);
        }
    }
}
