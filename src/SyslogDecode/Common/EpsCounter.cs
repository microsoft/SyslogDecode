// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Common
{
    using System;
    using System.Threading;

    /// <summary>Maintains event counter and computes EPS (events per second) on demand. </summary>
    public class EpsCounter
    {
        public readonly string Name;

        /// <summary>
        /// The current number of items in the counter.
        /// </summary>
        public long CurrentItemCount => _currentItemCount;

        private TimeSpan _minReadInterval; 
        private long _currentItemCount;
        private LastReadData _lastRead; 

        class LastReadData
        {
            internal DateTime DateTime;
            internal long CounterValue;
            internal int Eps;
        }

        public EpsCounter(string name, int minReadIntervalSec = 5)
        {
            Name = name;
            _minReadInterval = TimeSpan.FromSeconds(minReadIntervalSec);
            _lastRead = new LastReadData() { DateTime = AppTime.UtcNow }; 
        }

        /// <summary>
        /// Safely increments the CurrentItemCount.
        /// </summary>
        /// <returns></returns>
        public long Increment()
        {
            return Interlocked.Increment(ref _currentItemCount);
        }

        /// <summary>
        /// Safely adds the specified value to the CurrentItemCount.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public long Add(long value)
        {
            return Interlocked.Add(ref _currentItemCount, value);
        }

        /// <summary>
        /// Returns the current events per second.
        /// </summary>
        /// <returns>int - the current events per second.</returns>
        public int ReadEps()
        {
            // if not enough time since last read, return old value
            var utcNow = AppTime.UtcNow;
            var lastRead = _lastRead;
            var time = utcNow.Subtract(lastRead.DateTime);
            if (time < _minReadInterval)
                return lastRead.Eps; //return old value

            var curr = _currentItemCount;
            var eps = (curr - lastRead.CounterValue) / time.TotalSeconds;
            var intEps = (int)eps;
            _lastRead = new LastReadData() { CounterValue = curr, DateTime = utcNow, Eps = intEps };
            return intEps; 
        }

        /// <summary>
        /// Set current value and compute EPS. Use this overload when you don't increment counter but provide new value from outside.
        /// </summary>
        /// <param name="newCounterValue">long - the counter value to set the CurrentItemCount to. </param>
        /// <returns>int - the most recent events per second.</returns>
        public int ReadEps(long newCounterValue)
        {
            var curr = _currentItemCount; 
            if (newCounterValue < curr) //reset it
            {
                _currentItemCount = 0;
                _lastRead = new LastReadData() { CounterValue = curr, DateTime = AppTime.UtcNow};
                return 0; 
            }
            return ReadEps(); 
        }
    }
}
