using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    public class Compressor : Zipper
    {
        private volatile int _blockId;
        private volatile bool _isReadCompleted;
        private volatile bool _isActionCompleted;
        private volatile int _readCounter = 0;
        private object _locker;

        public Compressor(string sourceFile, string destinationFile) : base(sourceFile, destinationFile)
        {
            _blockId = 0;
            _isReadCompleted = false;
            _isActionCompleted = false;
            _locker = new object();
        }

        protected override void Read()
        {
            try
            {
                using (var fileToBeCompressed = new FileStream(_sourceFile, FileMode.Open))
                {
                    int bytesToRead;
                    byte[] lastBuffer;

                    while (fileToBeCompressed.Position < fileToBeCompressed.Length && !_cancelled)
                    {
                        if ((fileToBeCompressed.Length - fileToBeCompressed.Position) <= _blockSize)
                            bytesToRead = (int)(fileToBeCompressed.Length - fileToBeCompressed.Position);
                        else
                            bytesToRead = _blockSize;

                        lastBuffer = new byte[bytesToRead];
                        fileToBeCompressed.Read(lastBuffer, 0, bytesToRead);
                        var newBlock = new ByteBlock(_blockId, lastBuffer, new byte[0]);
                        _readerQueue.Enqueue(newBlock);
                        _blockId++;
                        _readCounter++;
                    }
                    _isReadCompleted = true;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected override void Write()
        {
            using (FileStream fileCompressed = new FileStream(_destinationFile, FileMode.Append))
            {
                int counter = 0;
                while (true && !_cancelled)
                {
                    if (_writerQueue.Count == 0 && !_isActionCompleted)
                        continue;

                    if (!_writerQueue.TryDequeue(out ByteBlock block))
                    {
                        if (_readCounter != counter)
                            continue;
                        else
                            break;
                    }

                    BitConverter.GetBytes(block.Buffer.Length).CopyTo(block.Buffer, 4);
                    fileCompressed.Write(block.Buffer, 0, block.Buffer.Length);
                    counter++;
                }

                _readerResetEvent.Set();
            }
        }

        protected override void Action(object i)
        {
            while (true && !_cancelled)
            {
                if (_readerQueue.Count == 0 && !_isReadCompleted)
                    continue;

                if (!_readerQueue.TryDequeue(out ByteBlock block))
                    break;

                using (MemoryStream stream = new MemoryStream())
                {
                    using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        gZipStream.Write(block.Buffer, 0, block.Buffer.Length);
                    }

                    byte[] compressedData = stream.ToArray();
                    ByteBlock outData = new ByteBlock(block.Id, compressedData, new byte[0]);

                    lock (_locker)
                    {
                        while (outData.Id != _currentBlockId)
                        {
                            Monitor.Wait(_locker);
                        }

                        _writerQueue.Enqueue(outData);
                        _currentBlockId++;
                        Monitor.PulseAll(_locker);
                    }
                }
            }

            _doneEvents[(int)i].Set();
            _isActionCompleted = true;
        }
    }
}
