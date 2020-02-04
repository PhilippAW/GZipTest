using System.Collections.Concurrent;
using System.Threading;

namespace GZipTest
{
    public class ByteBlockQueue
    {
        private ConcurrentQueue<ByteBlock> _queue = new ConcurrentQueue<ByteBlock>();
        private object _locker = new object();
        private int _currentBlockId = 0;
        private bool _isStopped = false;

        public void Enqueue(ByteBlock block)
        {
            _queue.Enqueue(block);
        }

        public int Count { get { return _queue.Count; } }

        public void EnqueueForWriting(ByteBlock block)
        {
            lock (_locker)
            {
                while (block.Id != _currentBlockId)
                    Monitor.Wait(_locker);

                _queue.Enqueue(block);
                _currentBlockId++;
                Monitor.PulseAll(_locker);
            }
        }

        public ByteBlock Dequeue()
        {
            lock (_locker)
            {
                while (_queue.Count == 0 && !_isStopped)
                    Monitor.Wait(_locker);

                if (_queue.TryDequeue(out ByteBlock block))
                    return block;

                return null;
            }
        }

        public void Stop()
        {
            lock (_locker)
            {
                _isStopped = true;
                Monitor.PulseAll(_locker);
            }
        }
    }
}
