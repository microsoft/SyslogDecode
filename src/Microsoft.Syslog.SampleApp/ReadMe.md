## Microsoft.Syslog.Sample demo application
The sample console application demonstrates use of server side Syslog processing pipeline. The app creates a syslog UDP listener on port 514 and syslog parsing pipeline. It then sends 100k messages in different formats using SyslogClient. The messages are received by the listener, parsed and reported to the host app through an event. Parsing includes detection of IP addresses (IPv4 and IPv6). The Demo app verifies the final received message count, and verifies that all IP addresses were detected. 

