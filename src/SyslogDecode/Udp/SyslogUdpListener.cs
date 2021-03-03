// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Udp
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using SyslogDecode.Common;
    using SyslogDecode.Model;

    /// <summary>
    /// Listens on the specified IP address and port, and broadcasts received UPD packets to any subscribed observers.
    /// </summary>
    public class SyslogUdpListener: Observable<RawSyslogMessage>, IDisposable
    {
        /// <summary>Default UDP buffer size, 1 Gb. </summary>
        public const int DefaultUdpBufferSize = 1 * 1024 * 1024 * 1024; // 1 GB
        public UdpClient PortListener { get; private set; }
        public event EventHandler<ErrorEventArgs> Error;
        public readonly EpsCounter InputEpsCounter = new EpsCounter("UdpEps");
        public long PacketCount => InputEpsCounter.CurrentItemCount;
        public event EventHandler<ItemEventArgs<RawSyslogMessage>> Received; 

        private Thread _thread; 
        private bool _running;
        private long _packetCount;
        private bool _disposeClientOnDispose;

        /// <summary>Creates a new instance of the SyslogUdpListener. </summary>
        /// <param name="address">The IP address, optional. Defaults to local address. Use it if you need to connect the listener to the port on a specific 
        /// network card with non-default local address.</param>
        /// <param name="port">UDP port, defaults to 514.</param>
        /// <param name="bufferSize">UDP buffer size. Defaults to 1 Gb, recommended for high-rate dedicated syslog servers.</param>
        public SyslogUdpListener(IPAddress address = null, int port = 514, int bufferSize = DefaultUdpBufferSize )
        {
            address = address ?? IPAddress.Parse("127.0.0.1");
            var endPoint = new IPEndPoint(address, port);
            PortListener = new UdpClient(endPoint);
            PortListener.Client.ReceiveBufferSize = bufferSize;
            _disposeClientOnDispose = true;
        }

        public SyslogUdpListener(UdpClient portListener)
        {
            PortListener = portListener;
            _disposeClientOnDispose = false;
        }

        /// <summary>Starts the listener. </summary>
        public void Start()
        {
            if (_running)
            {
                return; 
            }
            _running = true;

            // Important: we need real high-priority thread here, not pool thread from Task.Run()
            // Note: going with multiple threads here results in broken messages, the received message gets cut-off
            _thread = new Thread(RunListenerLoop);
            _thread.Priority = ThreadPriority.Highest;
            _thread.Start();
        }

        /// <summary>Stops the listener. Waits for draining the input UDP buffer befor closing the listener. </summary>
        public void Stop()
        {
            if (!_running)
                return;
            while (GetUdpBufferBytesAvailable() > 0)
                Thread.Sleep(10);
            PortListener.Close();
            _running = false;
            Thread.Sleep(10);
        }

        private void RunListenerLoop()
        {
            try
            {
                var remoteIp = new IPEndPoint(IPAddress.Any, 0);
                while (_running)
                {
                    var bytes = PortListener.Receive(ref remoteIp);
                    var message = Encoding.UTF8.GetString(bytes); // See notes on encoding at the end of this file
                    var msg = new RawSyslogMessage() { ReceivedOn = DateTime.UtcNow, SourceIpAddress = remoteIp.Address, Message = message };
                    Interlocked.Increment(ref _packetCount);
                    InputEpsCounter.Add(1); 
                    Broadcast(msg);
                    Received?.Invoke(this, new ItemEventArgs<RawSyslogMessage>(msg));
                }
            }
            catch (Exception ex)
            {
                if (!_running)
                    return; // it is closing socket
                OnError(ex); 
            }
        }

        private void OnError(Exception error)
        {
            Error?.Invoke(this, new ErrorEventArgs(error));
        }

        public void Dispose()
        {
            if (_disposeClientOnDispose && PortListener != null)
            {
                PortListener.Dispose();
                PortListener = null; 
            }
        }

        public int GetUdpBufferBytesAvailable()
        {
            try
            {
                if (PortListener == null)
                    return 0;
                return PortListener.Available;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Retrieves the health data.</summary>
        /// <param name="data">Data container.</param>
        /// <param name="prefix">Optional key prefix.</param>
        public void AddHealthData(IDictionary<string, object> data, string prefix = null)
        {
            data[prefix + DataKeyPacketCount] = PacketCount;
            data[prefix + DataKeyInputEps] = InputEpsCounter.ReadEps();
            data[prefix + DataKeyBufferBytes] = GetUdpBufferBytesAvailable();
        }

        public const string DataKeyPacketCount = "UdpPacketCount";
        public const string DataKeyInputEps = "UdpPacketPerSec";
        public const string DataKeyBufferBytes = "UdpBufferBytes";

    }
    /* Note about encoding.
    The RFC-5424 states that most of the syslog message should be encoded as plain ASCII string
    - except values of parameters in StructuredData section; these are allowed to be in Unicode/UTF-8. 
    Since 
       any valid ASCII text is valid UTF-8 text  
    ... we use UTF-8 for reading the payload, so we can read correctly the entire mix. 
    So in this case the recommendation in the RFC doc is primarily for writers (log producers) 
    to stay with ASCII most of the time, with occasional values in UTF-8. 
    We on the other hand, as reader, are 'forgiving', reading the entire text as UTF-8 message. 

    About BOM: the RFC doc states that [Message] portion of the payload (the tail part) can start with  
    the BOM - byte order mark - to indicate Unicode content. 
    We strip the BOM off when we parse the payload, as it brings troubles if left in the string
    - just from past experience, it is invisible, debugger does not show it, but it can break some string
    operations.
     */

}
