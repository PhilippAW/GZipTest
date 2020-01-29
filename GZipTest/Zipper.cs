﻿using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GZipTest
{
    public abstract class Zipper
    {
        protected string _sourceFile;
        protected string _destinationFile;
        protected int _currentBlockId;
        protected bool _cancelled = false;
        protected bool _success;

        protected int _blockSize = 10000000;
        protected ConcurrentQueue<ByteBlock> _readerQueue = new ConcurrentQueue<ByteBlock>();
        protected ConcurrentQueue<ByteBlock> _writerQueue = new ConcurrentQueue<ByteBlock>();

        protected ManualResetEvent _readerResetEvent;
        protected ManualResetEvent[] _doneEvents;

        public Zipper()
        {
            ThreadsCount = Environment.ProcessorCount;
            _doneEvents = new ManualResetEvent[ThreadsCount+1];
            _currentBlockId = 0;

            _readerResetEvent = new ManualResetEvent(false);
        }

        public Zipper(string sourceFile, string destinationFile) : this()
        {
            _sourceFile = sourceFile;
            _destinationFile = destinationFile;
        }

        protected int ThreadsCount { get; private set; }

        public void Start()
        {
            var reader = new Thread(new ThreadStart(Read));
            reader.Start();

            for (int i = 0; i < _doneEvents.Length-1; i++)
            {
                _doneEvents[i] = new ManualResetEvent(false);
                var q = new Thread(new ParameterizedThreadStart(Action));
                q.Start(i);
            }

            _doneEvents[_doneEvents.Length - 1] = _readerResetEvent;

            var writer = new Thread(new ThreadStart(Write));
            writer.Start();

            WaitHandle.WaitAll(_doneEvents);

            if (!_cancelled)
            {
                _success = true;
            }
        }

        public void Cancel()
        {
            _cancelled = true;
        }

        protected abstract void Read();

        protected abstract void Write();

        protected abstract void Action(object i);
    }
}
