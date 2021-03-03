## SyslogDecode.SampleApp Demo Application
This sample console application sets up a Syslog processing pipeline with UDP listening endpoint. It then sends a large array of pre-fabricated messages the this enpoint using a UDP sender component also provided by the *SyslogDecode* package. 

The test run verifies that number of messages received matches the number of messages sent. It also verifies that IP addresses embedded in messages are detected, extracted and reported in output structured records representing the parsed syslog messages. 
