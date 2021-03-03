// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Processes stream of messages on multiple parallel threads. </summary>
    public abstract class ParallelStreamProcessor<Tin, Tout> : Observable<Tout>, IObserver<Tin>
    {
        public int BatchSize;
        public int IdlePauseMs = 10;
        public int ThreadCount;

        public event EventHandler<ItemEventArgs<Tin>> ItemReceived;
        public event EventHandler<ItemEventArgs<Tout>> ItemProcessed;
        public readonly EpsCounter InputEpsCounter = new EpsCounter("InputEps");
        public readonly EpsCounter OutputEpsCounter = new EpsCounter("OutputEps"); 

        public long InputCount => InputEpsCounter.CurrentItemCount;
        public long OutputCount => OutputEpsCounter.CurrentItemCount;
        public int BufferQueueCount => _buffer.Count;
        public int ActiveProcessCount => _activeProcessCount;

        private readonly BatchingQueue<Tin> _buffer;
        private int _activeProcessCount; 
        private bool _running;

        public ParallelStreamProcessor(int batchSize = 100, int? threadCount = null)
        {
            BatchSize = batchSize;
            _buffer = new BatchingQueue<Tin>();
            if (threadCount == null)
                ThreadCount = Environment.ProcessorCount;
            else
                ThreadCount = threadCount.Value; 
        }

        public virtual void Start()
        {
            if (_running)
                return;
            _running = true;
            for(int i = 0; i < ThreadCount; i++)
            {
                var thread = new Thread(RunProcessingLoop);
                thread.Start(); 
            }
        }

        public virtual void Stop()
        {
            OnCompleted();
            _running = false;
            while(!IsIdle)
                Thread.Sleep(10); 
        }

        public bool IsIdle => BufferQueueCount == 0 && _activeProcessCount == 0;

        protected abstract void ProcessItem(Tin item);

        #region IObserver members
        public virtual void OnNext(Tin item)
        {
            _buffer.Enqueue(item);
            InputEpsCounter.Increment(); 
            if (!_running)
                Start();
            ItemReceived?.Invoke(this, new ItemEventArgs<Tin>(item));
        }

        public virtual void OnCompleted()
        {
            while (!IsIdle)
                Thread.Sleep(50);
        }

        public virtual void OnError(Exception error)
        {
            // errors upstream (in UDP listener) should be already reported
        }
        #endregion

        private void RunProcessingLoop()
        {
            while (_running)
            {
                if (_buffer.Count == 0)
                {
                    if (!_running)
                        return; 
                    Thread.Sleep(IdlePauseMs);
                    continue; 
                }
                // if not enough for a full batch, make another wait
                if (_buffer.Count < BatchSize / 2)
                    Thread.Sleep(IdlePauseMs);
                try
                {
                    Interlocked.Increment(ref _activeProcessCount);
                    // get batch
                    var items = _buffer.DequeueMany(BatchSize);
                    if (items.Count == 0)
                    {
                        continue;
                    }
                    ProcessBatch(items); 
                }
                catch (Exception ex)
                {
                    BroadcastError(ex); 
                } finally
                {
                    Interlocked.Decrement(ref _activeProcessCount);
                }
            }
        } //method

        protected virtual void ProcessBatch(IList<Tin> items)
        {
                // var start = AppTime.GetTimestamp();
                foreach (var item in items)
                {
                    try
                    {

                        ProcessItem(item);
                    }
                    catch (Exception ex)
                    {
                        BroadcastError(ex); 
                    }
                }
                //var timeMs = AppTime.GetDuration(start).TotalMilliseconds;
        }

        public override void Broadcast(Tout item)
        {
            OutputEpsCounter.Increment();
            base.Broadcast(item);
            ItemProcessed?.Invoke(this, new ItemEventArgs<Tout>(item));
        }

    }
}
