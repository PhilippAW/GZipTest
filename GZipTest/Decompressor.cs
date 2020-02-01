using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZipTest
{
    public class Decompressor : Zipper
    {
        private int _counter;
        private volatile bool _isReadCompleted;
        private volatile bool _isActionCompleted;
        private volatile int _readCounter;
        private object _locker;

        public Decompressor(string sourceFilePath, string destinationFilePath) : base(sourceFilePath, destinationFilePath)
        {
            _counter = 0;
            _isReadCompleted = false;
            _isActionCompleted = false;
            _locker = new object();
        }

        protected override void Read()
        {

            try
            {
                using (FileStream compressedFile = new FileStream(_sourceFile, FileMode.Open))
                {
                    while (compressedFile.Position < compressedFile.Length)
                    {
                        byte[] blockLengthBuffer = new byte[8];
                        compressedFile.Read(blockLengthBuffer, 0, blockLengthBuffer.Length);
                        int blockLength = BitConverter.ToInt32(blockLengthBuffer, 4);
                        byte[] compressedData = new byte[blockLength];
                        blockLengthBuffer.CopyTo(compressedData, 0);

                        compressedFile.Read(compressedData, 8, blockLength - 8);
                        int dataSize = BitConverter.ToInt32(compressedData, blockLength - 4);
                        byte[] lastBuffer = new byte[dataSize];

                        ByteBlock block = new ByteBlock(_counter, lastBuffer, compressedData);
                        _readerQueue.Enqueue(block);
                        _counter++;
                        _readCounter++;
                    }

                    _isReadCompleted = true;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                _isError = true;
            }
        }

        protected override void Write()
        {
            try
            {
                using (FileStream decompressedFile = new FileStream(_destinationFile, FileMode.Append))
                {
                    int counter = 0;
                    while (true && !_isCancelled)
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

                        decompressedFile.Write(block.Buffer, 0, block.Buffer.Length);
                        counter++;
                    }

                    _readerResetEvent.Set();
                }
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                _isError = true;
            }
        }

        protected override void Action(object i)
        {
            try
            {
                while (true && !_isCancelled)
                {
                    if (_readerQueue.Count == 0 && !_isReadCompleted)
                        continue;

                    if (!_readerQueue.TryDequeue(out ByteBlock block))
                        break;

                    using (MemoryStream stream = new MemoryStream(block.CompressedBuffer))
                    {
                        using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress))
                        {
                            gZipStream.Read(block.Buffer, 0, block.Buffer.Length);
                            var decompressedData = block.Buffer.ToArray();
                            ByteBlock block2 = new ByteBlock(block.Id, decompressedData, new byte[0]);

                            lock (_locker)
                            {
                                while (block2.Id != _currentBlockId)
                                {
                                    Monitor.Wait(_locker);
                                }

                                _writerQueue.Enqueue(block2);
                                _currentBlockId++;
                                Monitor.PulseAll(_locker);
                            }
                        }
                    }
                }

                _isActionCompleted = true;
                _doneEvents[(int)i].Set();
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                _isError = true;
            }
        }
    }
}
