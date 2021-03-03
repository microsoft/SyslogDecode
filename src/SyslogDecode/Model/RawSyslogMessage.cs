// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Model
{
    using System;
    using System.Net;

    /// <summary>A container for the raw, unparsed syslog message and related information.</summary>
    public class RawSyslogMessage
    {
        /// <summary>Date-time when the message was received. </summary>
        public DateTime ReceivedOn;

        /// <summary>The IP address of the message sender. </summary>
        public IPAddress SourceIpAddress;

        /// <summary>The message content. </summary>
        public string Message;
    }

}
