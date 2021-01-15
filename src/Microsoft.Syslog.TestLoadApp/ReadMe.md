# Microsoft.Syslog.TestLoadApp Tool
*TestLoadApp* is a simple console app for sending a test syslog stream from a *pcapng* file to the target UDP port at the specified IP address. The intended use is testing *Syslog* server components.  

You can sample the UDP traffic on a server and save messages in a file using the [WireShark](https://en.wikipedia.org/wiki/Wireshark) tool. Later you can use the the *TestLoadApp* tool to read the messages from the file and send it to the machine running the syslog server. You can set the desired MPS (messages per second) rate for the output stream.

## Running the Application
Prerequisite: You need have a *pcapng* file saved in a known location. See *WireShark* documentation on capturing the network traffic and saving it to a file. 

Edit the *app.config* file and set the configuration values: 
* *pcapFilePath* - a path to the pcapng file 
* *repeatCount* - a number of times to repeat the file read/send operation. Useful when you have a small sample file, but want a longer test duration and message volume. 
* *targetIp, targetPort* - the target IP address and UDP port to send the messages 
* *maxEps* - the maximum send rate, messages per second. 



  

 

 



