// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace Microsoft.Syslog
{
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using Microsoft.Syslog.Model;

    /// <summary>
    /// Client that sends SyslogEntry instances or UTF8 payloads to a host at the specified remote endpoint.
    /// <see cref="BufferedSyslogParser"/>
    /// </summary>
    public class SyslogClient
    {
        IPEndPoint _target; 
        UdpClient _udpClient;
        public UdpClient Client => _udpClient; 

        public SyslogClient(IPEndPoint target)
        {
            _target = target; 
            _udpClient = new UdpClient();
        }

        public SyslogClient(string ipAddress, int port = 514)
        {
            var addr = IPAddress.Parse(ipAddress);
            _target = new IPEndPoint(addr, port);
            _udpClient = new UdpClient();
        }

        /// <summary>
        /// Serializes SyslogEntry instance into a string for transmission and sends it over a network to an IP endpoint.
        /// </summary>
        /// <param name="entry">SyslogEntry - a SyslogEntry instance.</param>
        public void Send(ParsedSyslogMessage entry)
        {
            var payload = SyslogSerializer.Serialize(entry);
            Send(payload);
        }

        /// <summary>
        /// USe to send non-SyslogEntry messages.
        /// </summary>
        /// <param name="payload">string - the payload to send.</param>
        public void Send(string payload)
        {
            var dgram = Encoding.UTF8.GetBytes(payload);
            _udpClient.Send(dgram, dgram.Length, _target);
        }
    }
}
