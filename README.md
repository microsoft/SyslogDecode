# Microsoft.Syslog package

[Syslog](https://en.wikipedia.org/wiki/Syslog) is a logging protocol widely used in various networked devices.

The **Microsoft.Syslog** package provides components for building a Syslog processing server. The server listens to the syslog UDP port and processes the incoming messages. The server parses the messages, extracts the key values (timestamp, host server, IP addresses), and produces the output stream of strongly-typed records (of type [ParsedSyslogMessage](src/Microsoft.Syslog/Model/ParsedSyslogMessage.cs)) containing the detailed parsed information.

The parser/extractor is flexible regarding the contents and format of the input message. The set of detected key-values is not fixed, the output record contains a dictionary of key-value pairs which might be different for each message.

## Microsoft.Syslog components

* **SyslogUdpListener** - listens to the input stream on a local UDP port, a standard protocol for syslog transmission.
* **SyslogStreamParser** - high-performance parser consuming a stream of raw syslog messages and producing the stream of strongly-typed parsed records, ready for further analysis or uploading to the target log storage.
* **SyslogUdpPipeline** - a combination of the UDP listener and stream parser, ready-to-use processing pipeline for a UDP-listening server.
* **SyslogClient** - a simple Syslog client. Serializes the input (strongly-type) messages and sends them over the UDP protocol to the target listening server. Intended to for use primarily in testing of the Syslog server components.

Each component can be used independently. You can use the pipeline for parsing messages from different sources. The parser is customizable, you can add your own customizations to it.  

The components implement [IObserver\<T\>/IObservable\<T\>](https://docs.microsoft.com/en-us/dotnet/api/system.iobserver-1) interfaces, so they can be easily connected as stream processors. All components are thread-safe and free-threaded.

The **Microsoft.Syslog** package is heavily used in syslog processing coming from the entire Azure infrastructure (200K devices), had been proof-tested running for months under heavy load (100K messages per second) in a distributed, multi-node environment.  

## Projects and assemblies

This repository contains a Visual Studio solution with several projects:

* **Microsoft.Syslog** - the main implementation assembly, distributed as **Microsoft.Syslog** package.
* **Microsoft.Syslog.SampleApp** - a sample console application showing the use of a client component and server-side processing pipeline.
* **Microsoft.Syslog.TestLoadApp** - test load app. Allows you to send a test payload of syslog messages to the target IP/port. The messages are loaded from a file created by a network utility like [WireShark](https://en.wikipedia.org/wiki/Wireshark). You can capture the real traffic from your devices into a file, and then use this file as a test payload for the syslog server.
* **Microsoft.Syslog.Tests** - unit test project.

## Syslog message formats

Syslog is essentially a human readable text message, with some internal structure that is not always strictly followed.

Unfortunately, there is no established standard for syslog message format. The earliest attempt was [RFC-3164](https://tools.ietf.org/html/rfc3164), but it was more like overview of established practices than a real standard to follow. The other document is [RFC-5424](https://tools.ietf.org/html/rfc5424), much more rigorous specification, but not many device vendors follow this specification.

Finally, there is a key-value pairs format, used by some vendors (google 'Sophos syslog format').

In some cases the syslog message does not follow any prescribed structure, and can be viewed as plain text for human consumption.

Here are the percentages of message formats based on large number of messages from diverse devices in Azure:

Format | % messages
------ | -----------
RFC-5424 |  16
RFC-3164 |  60
Key-value pairs | 20
Others, plain text | 4

Given this absence of established standards, the challenge is make a best guess and to extract the important values like IP addresses or host names, so these values can be later used in analysis tools, or queried in log storage systems like Kusto. The result of the parsing is a structured record (SyslogEntry) that contains the information - timestamps, hostname, free-form message, IP addresses (IPv4 and IPv6).

The syslog parser makes a best guess about the format of the message. The detected format is available in enum value *ParsedSyslogMessage.PayloadType* of the parsed message.
