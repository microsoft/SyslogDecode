# SyslogDecode package

[Syslog](https://en.wikipedia.org/wiki/Syslog) is a logging protocol widely used in the industry. Syslog uses a client-server architecture where a syslog **server** listens for and logs messages coming from clients over the network.

The **SyslogDecode** package implements the components for building a syslog processing server. The server parses the input messages; it extracts the key values - a timestamp, host server, IP addresses, etc, and produces the output stream of strongly-typed records containing the message data. 

## Parsing/Decoding example
Here is an example of what kind of decoding the **SyslogDecode** does. A syslog message is a plain, somewhat structured text: 
```
<14>Mar  3 20:27:49 SDX3-5231-0200-2X0 snmp#supervisord: snmp-subagent WARNING:sonic_ax_impl:Invalid mgmt IP 22.111.33.44,2234:23d5:e0:25a3::55
``` 

This string is parsed into a structure represented by the following json: 

```json
{
  "DeviceTimestamp": "2021-03-03 20:27:49.00",
  "Facility": "UserLevel",
  "Severity": "Informational",
  "HostName": "SDX3-5231-0200-2X0",
  "AppName": "",
  "MsgId": "",
  "ExtractedData": {
    "IPv4": [
      "22.111.33.44"
    ],
    "IPv6": [
      "2234:23d5:e0:25a3::55"
    ]
  }  
}
```
As you see, the *Decoder* identified 'typical' syslog fields (datetime, hostname), decoded facility and severity, and also extracted IPv4 and IPv6 addresses from the plain Warning text at the end of the message.  

## Basic Usage 
### Server 

To setup a server listening to incoming messages on a local port, create an instance of the *SyslogUdpPipeline*: 

```csharp
class Program
{
  static SyslogUdpPipeline pipeline;
  
  static void Main(string[] args)
  {
    pipeline = new SyslogUdpPipeline();
    ... ... 
  }
}  
```

The code uses all default values in optional parameters of the constructor. It sets up the UDP port listener on local port 514 (standard for syslog); creates a default syslog parser and connects it to the listener. There are several optional parameters in pipeline constructor that allow you to specify values other than defaults. 
  
To listen to the output stream of parsed messages, you can subscribe an observer (handler), a class implementating the *IObserver\<ParsedSyslogMessage\>* interface, to the output of the stream parser:  

```csharp
    pipeline.StreamParser.Subscribe(parsedStreamHandler);
```

An example of a handler would be a component that uploads the parsed messages to the persistent storage. 

The other way to listen to the output stream is by handling an output event:  
 
```csharp
  // setup 
  pipeline.StreamParser.ItemProcessed += StreamParser_ItemProcessed;
  
  private static void StreamParser_ItemProcessed(object sender, ItemEventArgs<ParsedSyslogMessage> e)
  {
    var msg = e.Item;
    Console.WriteLine($"Host {msg.Header.HostName}, message: {msg.Message}");
  }
```

Once you finish setting up the pipeline, you must call the *Start* method:
 
```csharp
    pipeline.Start(); 
```

You can use the syslog stream parser for processing messages that come from any source, not necessarilly from UDP port. You can instantiate the stream parser component directly, and feed it a stream of raw syslog messages: 
 
```csharp
public static void ParseMessages(string[] messages, IObserver<ParsedSyslogMessage> consumer)
{
    var streamParser = new SyslogStreamParser(
         parser: SyslogMessageParser.CreateDefault(), // - default message parser, 
                                                      //   you can customize it
        threadCount: 10 // number of threads to use in parallel parsing
        );
    streamParser.Subscribe(consumer); 
    streamParser.Start();
    foreach(var msg in messages)
    {
        var rawMessage = new RawSyslogMessage() 
               {Message = msg,  ReceivedOn = DateTime.Now};
        streamParser.OnNext(rawMessage);
    }
    streamParser.OnCompleted(); // drain all queues
    streamParser.Unsubscribe(consumer); 
}
```

### SyslogUdpSender (Syslog client)
The *SyslogUdpSender* is a simple component that sends the syslog messages over UDP protocol to the target endpoint. You can use this component to implement a simple logging  facility in your application. It is also useful in testing the syslog server components to implement a test stream. 

The following code creates a sender and sends a number of messages to the target IP/port:   

```csharp
public void SendMessages(string targetIp, int targetPort, string[] messages)
{
    using (var sender = new SyslogUdpSender(targetIp, targetPort))
    {
        foreach (var msg in messages)
        {
            sender.Send(msg);
        }
    }
}
```

## Major components in SyslogDecode

* **SyslogMessageParser** - a customizable core parser of syslog messages. 
* **SyslogStreamParser** - high-performance parsing engine consuming a stream of raw syslog messages and producing the stream of strongly-typed parsed records, ready for further analysis or uploading to the target log storage. Uses *SyslogMessageParser* for parsing individual messages.
* **SyslogUdpListener** - listens to the input stream on a local UDP port, a standard protocol for syslog transmission.
* **SyslogUdpPipeline** - a combination of the UDP listener and stream parser, ready-to-use processing pipeline for a UDP-listening server.
* **SyslogUdpSender** - a simple Syslog message sender. Sends the messages over the UDP protocol to the target listening server. Intended to for use primarily in testing of the Syslog server components. 

Each component can be used independently. You can use the pipeline for parsing messages from different sources. The parser is customizable, you can add your own customizations to it.  

The components implement [IObserver\<T\>/IObservable\<T\>](https://docs.microsoft.com/en-us/dotnet/api/system.iobserver-1) interfaces, so they can be easily connected as stream processors. All components are thread-safe and free-threaded.

The **SyslogDecode** package had been battle-tested processing real high-volume message streams in Azure infrastructure.  

## Syslog Formats
Syslog is essentially a human readable text message, with some internal structure that is not always strictly followed. There is no established standard for syslog message format. The earliest attempt was [RFC-3164](https://tools.ietf.org/html/rfc3164), but it was more like overview of established practices than a real standard to follow. The other document is [RFC-5424](https://tools.ietf.org/html/rfc5424), much more rigorous specification, but not all log providers follow this specification.

There is also a key-value pairs format, used by some vendors (google 'Sophos syslog format'). In some cases messages do not follow any prescribed structure, and can be viewed as a plain text for human consumption.

Given this absence of established standards, the challenge is make a best guess and to extract the important values like IP addresses or host names, so these values can be later used in analysis tools or queried in log storage systems like Kusto. The parser in *SyslogDecode* detects/guesses the input message format, parses the message and extracts the information from it. 
 
## Contents of this this repository - core library, tests, samples and tools
This repository contains the following projects: 
* *SyslogDecode* - the source code of the main *SyslogDecode* assembly/package. 
* *SyslogDecode.Tests* - unit/integration tests for the components.
* *SyslogDecode.SampleApp* - a sample console application, sets up local UDP-fed syslog processing pipeline and sends a batch of simulated syslog message to the port. Verifies message counts, verifies that IP addresses were detected correctly.
* *SyslogDecode.TestLoadApp* - a command line tool to send events from a *pcapng* file to the target UDP port. It can be used to test the server pipeline with real syslog messages. *Pcapng* file might be produced by capturing the syslog traffic on a network using a tool like [WireShark](https://en.wikipedia.org/wiki/Wireshark).
