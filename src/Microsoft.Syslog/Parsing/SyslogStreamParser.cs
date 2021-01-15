// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace Microsoft.Syslog.Parsing
{
    using Microsoft.Syslog.Common;
    using Microsoft.Syslog.Model;
    using System.Collections.Generic;

    /// <summary>Parses input stream of raw syslog messages on multiple parallel threads. </summary>
    public class SyslogStreamParser : ParallelStreamProcessor<RawSyslogMessage, ParsedSyslogMessage>
    {
        public readonly SyslogMessageParser Parser;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parser"></param>
        /// <param name="batchSize"></param>
        /// <param name="threadCount"></param>
        public SyslogStreamParser(SyslogMessageParser parser = null, int batchSize = 100, int? threadCount = null): base(batchSize, threadCount)
        {
            Parser = parser ?? SyslogMessageParser.CreateDefault();
        }

        protected override void ProcessItem(RawSyslogMessage item)
        {
            var parsedMsg = Parser.Parse(item);
            Broadcast(parsedMsg); 
        }

        /// <summary>Retrieves the heartbeat data.</summary>
        /// <param name="data">Data container.</param>
        public void OnHeartbeat(IDictionary<string, object> data, string prefix = null)
        {
            data[prefix + DataKeyBufferQueueCount] = base.BufferQueueCount;
            data[prefix + DataKeyActiveParseProcessCount] = base.ActiveProcessCount;
            data[prefix + DataKeyInputEps] = base.InputEpsCounter.ReadEps();
            data[prefix + DataKeyOutputEps] = base.OutputEpsCounter.ReadEps();
        }

        public const string DataKeyBufferQueueCount = "Parser_BufferQueueCount";
        public const string DataKeyActiveParseProcessCount = "Parser_ActiveProcessCount";
        public const string DataKeyInputEps = "Parser_InputEps";
        public const string DataKeyOutputEps = "Parser_OutputEps";
    }
}
