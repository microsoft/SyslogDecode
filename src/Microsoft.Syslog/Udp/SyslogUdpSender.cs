// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace Microsoft.Syslog.Udp
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using Microsoft.Syslog.Model;

    /// <summary>
    /// Sends a stream of Syslog messages to the target remote endpoint.
    /// <see cref="BufferedSyslogParser"/>
    /// </summary>
    public class SyslogUdpSender: IDisposable
    {
        IPEndPoint _target; 
        UdpClient _udpClient;
        public UdpClient UdpClient => _udpClient; 

        public SyslogUdpSender(IPEndPoint target)
        {
            _target = target; 
            _udpClient = new UdpClient();
        }

        public SyslogUdpSender(string ipAddress, int port = 514)
        {
            var addr = IPAddress.Parse(ipAddress);
            _target = new IPEndPoint(addr, port);
            _udpClient = new UdpClient();
        }

        public void Dispose()
        {
            _udpClient.Dispose();
        }

        /// <summary>
        /// Serializes syslog record into a string for transmission and sends it over a network to an IP endpoint.
        /// </summary>
        /// <param name="entry">SyslogEntry - a SyslogEntry instance.</param>
        public void Send(ParsedSyslogMessage entry)
        {
            var payload = SyslogSerializer.Serialize(entry);
            Send(payload);
        }

        /// <summary>Sends plain text syslog message. </summary>
        /// <param name="payload">string - the payload to send.</param>
        public void Send(string payload)
        {
            var dgram = Encoding.UTF8.GetBytes(payload);
            _udpClient.Send(dgram, dgram.Length, _target);
        }
    }
}
