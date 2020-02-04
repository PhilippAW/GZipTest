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
        private int _blocksCount;

        public Decompressor(string sourceFilePath, string destinationFilePath) : base(sourceFilePath, destinationFilePath)
        {
            _counter = 0;
            _isReadCompleted = false;
        }

        protected override void Read()
        {
            try
            {
                using (FileStream compressedFile = new FileStream(_sourceFile, FileMode.Open))
                {
                    while (compressedFile.Position < compressedFile.Length)
                    {
                        if (_readerQueue2.Count > 10)
                            continue;

                        byte[] blockLengthBuffer = new byte[8];
                        compressedFile.Read(blockLengthBuffer, 0, blockLengthBuffer.Length);
                        int blockLength = BitConverter.ToInt32(blockLengthBuffer, 4);
                        byte[] compressedData = new byte[blockLength];
                        blockLengthBuffer.CopyTo(compressedData, 0);

                        compressedFile.Read(compressedData, 8, blockLength - 8);
                        int dataSize = BitConverter.ToInt32(compressedData, blockLength - 4);
                        byte[] lastBuffer = new byte[dataSize];

                        ByteBlock block = new ByteBlock(_counter, lastBuffer, compressedData);
                        _readerQueue2.Enqueue(block);
                        _counter++;
                    }

                    _blocksCount = _counter - 1;
                    _readerQueue2.Stop();
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Decompression.Read: {ex.Message}";
                _isError = true;
            }
            finally
            {
                _readerResetEvent.Set();
                _isReadCompleted = true;
            }
        }

        protected override void Write()
        {
            try
            {
                using (FileStream decompressedFile = new FileStream(_destinationFile, FileMode.Append))
                {
                    int lastBlockId = 0;

                    while (true && !_isCancelled && !_isError)
                    {
                        var block = _writerQueue2.Dequeue();
                        if (block == null)
                            break;

                        decompressedFile.Write(block.Buffer, 0, block.Buffer.Length);
                        Console.WriteLine($"Written block {block.Id}");
                        decompressedFile.Flush();
                        lastBlockId = block.Id;

                        if (lastBlockId == _blocksCount && _isReadCompleted)
                            break;
                    }

                    _writerQueue2.Stop();
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Decompression.Write: {ex.Message}";
                _isError = true;

            }
            finally
            {
                _writerResetEvent.Set();
            }
        }

        protected override void Action(object i)
        {
            try
            {
                while (true && !_isCancelled)
                {
                    if (_readerQueue2.Count == 0 && !_isReadCompleted)
                        continue;

                    var block = _readerQueue2.Dequeue();
                    if (block == null)
                        break;

                    using (MemoryStream stream = new MemoryStream(block.CompressedBuffer))
                    {
                        using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress))
                        {
                            gZipStream.Read(block.Buffer, 0, block.Buffer.Length);
                            var decompressedData = block.Buffer.ToArray();
                            ByteBlock block2 = new ByteBlock(block.Id, decompressedData, new byte[0]);
                            _writerQueue2.EnqueueForWriting(block2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Decompression: {ex.Message}";
                _isError = true;
            }
            finally
            {
                _doneEvents[(int)i].Set();
            }
        }
    }
}
