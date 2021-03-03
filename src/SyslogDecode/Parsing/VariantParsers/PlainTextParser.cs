// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Parsing
{
    using System;

    public class PlainTextParser : ISyslogVariantParser
    {
        public bool TryParse(ParserContext ctx)
        {
            ctx.SkipSpaces();
            ctx.ParsedMessage.PayloadType = Model.PayloadType.PlainText; 
            ctx.ParsedMessage.Message = ctx.Text.Substring(ctx.Position);
            ctx.ParsedMessage.Header.Timestamp = DateTime.UtcNow; 
            return true; 
        }
    }
}
