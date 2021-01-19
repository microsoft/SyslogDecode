# Microsoft.Syslog package

[Syslog](https://en.wikipedia.org/wiki/Syslog) is a logging protocol widely used in the industry. Syslog uses a client-server architecture where a syslog **server** listens for and logs messages coming from clients over the network.

The **Microsoft.Syslog** package implements the components for building a syslog processing server. The server parses the input messages; it extracts the key values - a timestamp, host server, IP addresses, etc, and produces the output stream of strongly-typed records containing the message data. 

## Installation
Install the latest stable binaries via [NuGet](https://www.nuget.org/packages/Microsoft.Syslog/).
```
> dotnet add package Microsoft.Syslog
```

## Basic Usage 
### Server 

To setup a server listening to incoming messages on a local port, create an instance of the *SyslogUdpPipeline*: 
```csharp
  public void Setup() 
  {
    this.pipeline = new SyslogUdpPipeline();
  }
```

The code uses all default values in optional parameters of the constructor. It sets up the UDP port listener on local port 514 (standard for syslog); creates a default syslog parser and connects it to the listener. There are several optional parameters in pipeline constructor that allow you to specify values other than defaults. 
  
To listen to the output stream of parsed messages, you can subscribe an observer (handler), a class implementating the *IObserver\<ParsedSyslogMessage\>* interface, to the output of the stream parser:  

```csharp
    this.pipeline.StreamParser.Subscribe(parsedStreamHandler);
```

An example of a handler would be a component that uploads/saves the messages to the persistent storage. 

The other way to listen to the output stream is by handling an output event:  
 
```csharp
  // setup 
  this.pipeline.StreamParser.ItemProcessed += StreamParser_ItemProcessed;
  
  private static void StreamParser_ItemProcessed(object sender, ItemEventArgs<ParsedSyslogMessage> e)
  {
    var msg = e.Item;
    Console.WriteLine($"Host {msg.Header.HostName}, message: {msg.Message}");
  }
```

Once you finish setting up the pipeline, you must call the *Start* method:
 
```csharp
    this.pipeline.Start(); 
```

You can use the syslog stream parser for processing messages that come from any source, not necessarilly from UDP port. You can instantiate the stream parser component directly, and feed it a stream of raw syslog messages: 
 
```csharp
public void ParseMessages(string[] messages, IObserver<ParsedSyslogMessage> consumer)
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
    streamParser.Unsubscribe(consumer); 
    streamParser.OnCompleted(); // drain all queues
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

## Major components

* **SyslogMessageParser** - a customizable core parser of syslog messages. 
* **SyslogStreamParser** - high-performance parsing engine consuming a stream of raw syslog messages and producing the stream of strongly-typed parsed records, ready for further analysis or uploading to the target log storage. Uses *SyslogMessageParser* for parsing individual messages.
* **SyslogUdpListener** - listens to the input stream on a local UDP port, a standard protocol for syslog transmission.
* **SyslogUdpPipeline** - a combination of the UDP listener and stream parser, ready-to-use processing pipeline for a UDP-listening server.
* **SyslogUdpSender** - a simple Syslog message sender. Sends the messages over the UDP protocol to the target listening server. Intended to for use primarily in testing of the Syslog server components. 

Each component can be used independently. You can use the pipeline for parsing messages from different sources. The parser is customizable, you can add your own customizations to it.  

The components implement [IObserver\<T\>/IObservable\<T\>](https://docs.microsoft.com/en-us/dotnet/api/system.iobserver-1) interfaces, so they can be easily connected as stream processors. All components are thread-safe and free-threaded.

The **Microsoft.Syslog** package is heavily used in syslog processing coming from the entire Azure infrastructure (200K devices), had been proof-tested running for months under heavy load (100K messages per second) in a distributed, multi-node environment.  

## Syslog Formats
Syslog is essentially a human readable text message, with some internal structure that is not always strictly followed. There is no established standard for syslog message format. The earliest attempt was [RFC-3164](https://tools.ietf.org/html/rfc3164), but it was more like overview of established practices than a real standard to follow. The other document is [RFC-5424](https://tools.ietf.org/html/rfc5424), much more rigorous specification, but not many log providers follow this specification.

There is also a key-value pairs format, used by some vendors (google 'Sophos syslog format'). And in some cases the syslog message does not follow any prescribed structure, and can be viewed as a plain text for human consumption.
Given this absence of established standards, the challenge is make a best guess and to extract the important values like IP addresses or host names, so these values can be later used in analysis tools, or queried in log storage systems like Kusto. The parser in *Micorosoft.Syslog* detects the input message format, parses the message and extracts the information from it. 
 

