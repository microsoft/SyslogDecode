// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace Microsoft.Syslog.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Syslog.Model;

    /// <summary>
    ///    Configurable syslog message parser for multiple variants (RFCs) and extractors. 
    /// </summary>
    /// <remarks>
    ///    Holds configurable lists of version parsers (for handling specific version/format) and 
    ///    list of value extractors for pattern-based extraction.   
    /// </remarks>
    public class SyslogMessageParser
    {
        public readonly List<ISyslogVariantParser> VariantParsers = new List<ISyslogVariantParser>();
        public readonly List<IValuesExtractor> ValueExtractors = new List<IValuesExtractor>();

        /// <summary> Creates and configures a default parser, with support for all major syslog versions 
        ///     and IP addresses extractor. 
        /// </summary>
        /// <returns></returns>
        public static SyslogMessageParser CreateDefault()
        {
            var parser = new SyslogMessageParser();
            parser.AddVariantParsers(new Rfc5424SyslogParser(), new KeyValueListParser(), 
                                     new Rfc3164SyslogParser(), new PlainTextParser());
            parser.AddValueExtractors(new IpAddressesExtractor());
            return parser;
        }

        public void AddVariantParsers(params ISyslogVariantParser[] parsers)
        {
            VariantParsers.AddRange(parsers);
        }

        public void AddValueExtractors(params IValuesExtractor[] extractors)
        {
            ValueExtractors.AddRange(extractors); 
        }

        public ParsedSyslogMessage Parse(RawSyslogMessage rawMessage)
        {
            var ctx = new ParserContext(rawMessage);
            Parse(ctx);
            return ctx.ParsedMessage; 
        }


        public void Parse(ParserContext context)
        {
            if (!context.ReadSyslogPrefix())
                return;
            context.AssignFacilitySeverity();

            foreach (var parser in VariantParsers)
            {
                context.Reset(); 
                try
                {
                    if (parser.TryParse(context))
                    {
                        ExtractDataFromMessage(context);
                        BuildAllDataDictionary(context.ParsedMessage); // put all parsed/extracted data into AllData dictionary
                        return; 
                    }
                }
                catch (Exception ex)
                {
                    context.ErrorMessages.Add(ex.ToString() + " message: " + context.Text);
                    ex.Data["SyslogMessage"] = context.Text;
                    throw; 
                }
            }
        }

        private void ExtractDataFromMessage(ParserContext ctx)
        {
            var entry = ctx.ParsedMessage;
            var data = entry.ExtractedTuples;
            // For RFC-5424 and KeyValue payload types, everything is already structured and extracted
            // But we want to run IP values detector against all of them
            switch (entry.PayloadType)
            {
                case PayloadType.Rfc5424:
                    var allParams = entry.StructuredData5424.SelectMany(e => e.Value).ToList();
                    var Ips = IpAddressesExtractor.ExtractIpAddresses(allParams);
                    data.AddRange(Ips);
                    return; 

                case PayloadType.KeyValuePairs:
                    var Ips2 = IpAddressesExtractor.ExtractIpAddresses(entry.ExtractedTuples);
                    data.AddRange(Ips2); 
                    return; 
            }

            // otherwise run extractors from plain message
            if (entry == null || string.IsNullOrWhiteSpace(entry.Message) || entry.Message.Length < 10)
                return;

            foreach(var extr in ValueExtractors)
            {
                var values = extr.ExtractValues(ctx); 
                if (values != null && values.Count > 0)
                {
                    data.AddRange(values); 
                }
            }
        }

        /// <summary>Merges all parsed and extracted data into a single Data dictionary. </summary>
        /// <param name="message"></param>
        private void BuildAllDataDictionary(ParsedSyslogMessage message)
        {
            // ExtractedData is key-values extracted from syslog body by extractors
            // entry.StructuredData is RFC-5424 structured data; 
            var allTuples = new List<NameValuePair>();
            allTuples.AddRange(message.ExtractedTuples);

            // This is RFC-5424 specific data, structured in its own way - we take all parameters (kv pairs) from there
            var structData = message.StructuredData5424;
            if (structData != null)
            {
                var structDataParams = structData.SelectMany(de => de.Value).ToList();
                allTuples.AddRange(structDataParams);
            }

            // now convert to dictionary; first group values by name
            var groupedTuples = allTuples.GroupBy(p => p.Name, p => p.Value).ToDictionary(g => g.Key, g => g.ToArray());
            // copy to final dictionary - keep multiple values as an array; 
            // for single value put it as a single object (not object[1]); - except for IP addresses - these are always arrays, even if there's only one
            message.Data.Clear();
            foreach (var kv in groupedTuples)
            {
                if (kv.Value.Length > 1 || kv.Key == "IPv4" || kv.Key == "IPv6")
                {
                    message.Data[kv.Key] = kv.Value; //array
                }
                else
                    message.Data[kv.Key] = kv.Value[0]; //single value
            }
        }


    }
}
