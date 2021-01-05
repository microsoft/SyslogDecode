using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Syslog.Common
{
    public class ItemEventArgs<T>: EventArgs
    {
        public readonly T Item; 
        public ItemEventArgs(T item)
        {
            Item = item; 
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        public readonly Exception Error;
        public ErrorEventArgs(Exception error)
        {
            Error = error; 
        }
    }
}
