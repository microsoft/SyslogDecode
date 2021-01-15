// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace Microsoft.Syslog.TestLoadApp
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Microsoft.Syslog;
    using Microsoft.Syslog.Model;
    using Microsoft.Syslog.Udp;
    using Tx.Network;
    using Tx.Network.Snmp;

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var appStt = ConfigurationManager.AppSettings; 
                var targetIp = appStt["targetIp"];
                var port = int.Parse(appStt["targetPort"]);
                var pcapFile = appStt["pcapFilePath"];
                var maxEps = int.Parse(appStt["maxEps"]);
                var repeatCount = int.Parse(appStt["repeatCount"]);

                Console.WriteLine($" Sending pcap file '{pcapFile}', repeatCount: {repeatCount}, target IP/port: {targetIp}:{port}");
                PCapFileProcessor.SendFile(pcapFile, repeatCount, maxEps, int.MaxValue, targetIp, port);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            } finally
            {
                Console.WriteLine($"Done.");
                Console.WriteLine($"Press any key...");
                Console.ReadKey();
            }
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
            Console.WriteLine($"Sending syslog messages from file {pcapFile} ...");

            string ipAddress = ConfigurationManager.AppSettings["targetIp"];
            var client = new SyslogUdpSender(ipAddress);
            Console.WriteLine($"Local IP address: {ipAddress}");

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