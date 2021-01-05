// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace Microsoft.Syslog.Udp
{
    using System;
    using System.Net;
    using System.Threading;
    using Microsoft.Syslog.Common;
    using Microsoft.Syslog.Parsing;

    public class SyslogUdpPipeline
    {
        public readonly SyslogUdpListener UdpListener;
        public readonly SyslogStreamParser StreamParser;
        public event EventHandler<ErrorEventArgs> Error;

        public SyslogUdpPipeline(IPAddress ipAddress, int port = 514, SyslogMessageParser parser = null)
            : this(new SyslogUdpListener(ipAddress, port), parser)
        {

        }

        public SyslogUdpPipeline(SyslogUdpListener udpListener, SyslogMessageParser parser = null)
        {
            UdpListener = udpListener;
            StreamParser = new SyslogStreamParser(parser);
            UdpListener.Subscribe(StreamParser);
        }

        public void Start()
        {
            UdpListener.Start();
            StreamParser.Start(); 
        }

        public void Stop()
        {
            UdpListener.Stop();
            Thread.Sleep(20);
            // Waiting for all buffers and queues are clear and all active parsing threads completed. 
            StreamParser.Stop(); 
        }

        public void Dispose()
        {
            UdpListener.Dispose();
        }

        private void UdpListener_Error(object sender, ErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }

    }
}
