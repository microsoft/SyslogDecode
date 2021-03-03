// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Udp
{
    using System;
    using System.Net;
    using System.Threading;
    using SyslogDecode.Common;
    using SyslogDecode.Parsing;

    /// <summary>Syslog listening/processing pipeling. Sets up a UDP listener and stream parser connected to the listener. </summary>
    public class SyslogUdpPipeline
    {
        public readonly SyslogUdpListener UdpListener;
        public readonly SyslogStreamParser StreamParser;
        public event EventHandler<ErrorEventArgs> Error;

        /// <summary>Creates a new instance of the class. </summary>
        /// <param name="ipAddress">The IP address for the input UDP port, optional; defaults to local IP.</param>
        /// <param name="port">UDP port, defaults to 514.</param>
        /// <param name="parser">Optional, a message parser instance. If not provided, a default parser is used.</param>
        public SyslogUdpPipeline(IPAddress ipAddress = null, int port = 514, SyslogMessageParser parser = null)
            : this(new SyslogUdpListener(ipAddress, port), parser)
        {
        }

        /// <summary>Creates a new instance of the class from UDP listener and message parser instances.. </summary>
        /// <param name="udpListener">UDP listener instance.</param>
        /// <param name="parser">Optional, a message parser instance. If not provided, a default parser is used.</param>
        public SyslogUdpPipeline(SyslogUdpListener udpListener, SyslogMessageParser parser = null)
        {
            UdpListener = udpListener;
            StreamParser = new SyslogStreamParser(parser);
            UdpListener.Subscribe(StreamParser);
        }

        /// <summary>Starts the pipeline, activates the UDP listener.  </summary>
        public void Start()
        {
            UdpListener.Start();
            StreamParser.Start(); 
        }

        /// <summary>Stops the pipeline. Waits for draining all internal queues before returning. </summary>
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
