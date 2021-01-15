using Microsoft.Syslog.Model;
using Microsoft.Syslog.Parsing;
using Microsoft.Syslog.Udp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Syslog.Tests
{
    class __ReadMeSamples
    {
        public void SendMessages(string targetIp, int targetPort, string[] messages)
        {
            var sender = new SyslogUdpSender(targetIp, targetPort); 
            foreach(var msg in messages)
            {
                sender.Send(msg); 
            }
        }

        public void ParseMessages(string[] messages, IObserver<ParsedSyslogMessage> consumer)
        {
            var streamParser = new SyslogStreamParser(
                parser: SyslogMessageParser.CreateDefault(), // default message parser, you can customize it
                batchSize: 100, // number of messages to grab from the input queue, per thread
                threadCount: 10 // number of threads to use in parallel parsing
                );
            streamParser.Subscribe(consumer); 
            streamParser.Start();
            foreach(var msg in messages)
            {
                var rawMessage = new RawSyslogMessage()
                     {  Message = msg,  ReceivedOn = DateTime.Now};
                streamParser.OnNext(rawMessage);
            }
            streamParser.Unsubscribe(consumer); 
            streamParser.OnCompleted(); // drain all queues
        }

    }
}
