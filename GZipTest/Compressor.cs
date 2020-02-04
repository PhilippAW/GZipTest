using System;
using System.IO;
using System.IO.Compression;

namespace GZipTest
{
    public class Compressor : Zipper
    {
        private volatile int _blockId;
        private volatile bool _isReadCompleted;
        private int _blocksCount;

        public Compressor(string sourceFile, string destinationFile) : base(sourceFile, destinationFile)
        {
            _blockId = 0;
            _isReadCompleted = false;
        }

        protected override void Read()
        {
            try
            {
                using (var fileToBeCompressed = new FileStream(_sourceFile, FileMode.Open))
                {
                    int bytesToRead;
                    byte[] lastBuffer;

                    while (fileToBeCompressed.Position < fileToBeCompressed.Length && !_isCancelled)
                    {
                        if (_readerQueue2.Count > 10)
                            continue;

                        if ((fileToBeCompressed.Length - fileToBeCompressed.Position) <= _blockSize)
                            bytesToRead = (int)(fileToBeCompressed.Length - fileToBeCompressed.Position);
                        else
                            bytesToRead = _blockSize;

                        lastBuffer = new byte[bytesToRead];
                        fileToBeCompressed.Read(lastBuffer, 0, bytesToRead);
                        var newBlock = new ByteBlock(_blockId, lastBuffer, new byte[0]);
                        _readerQueue2.Enqueue(newBlock);
                        _blockId++;
                    }

                    _blocksCount = _blockId-1;
                    _readerQueue2.Stop();
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Compression.Read: {ex.Message}";
                _isError = true;
            }
            finally
            {
                Console.WriteLine($"Read exit");
                _readerResetEvent.Set();
                _isReadCompleted = true;
            }
        }

        protected override void Write()
        {
            try
            {
                using (FileStream fileCompressed = new FileStream(_destinationFile, FileMode.Append))
                {
                    int lastBlockId = 0;
                    while (true && !_isCancelled && !_isError)
                    {
                        var block = _writerQueue2.Dequeue();
                        if (block == null)
                            break;

                        BitConverter.GetBytes(block.Buffer.Length).CopyTo(block.Buffer, 4);
                        fileCompressed.Write(block.Buffer, 0, block.Buffer.Length);
                        Console.WriteLine($"{block.Id}");
                        fileCompressed.Flush();
                        lastBlockId = block.Id;

                        if (lastBlockId == _blocksCount && _isReadCompleted)
                            break;
                    }
                    _writerQueue2.Stop();
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Compression.Write: {ex.Message}";
                _isError = true;
            }
            finally 
            {
                Console.WriteLine($"Write exit");
                _writerResetEvent.Set();
            }
        }

        protected override void Action(object i)
        {
            try
            {
                while (true && !_isCancelled && !_isError)
                {
                    if (_readerQueue2.Count == 0 && !_isReadCompleted)
                        continue;

                    ByteBlock block = _readerQueue2.Dequeue();
                    if (block == null )
                        break;

                    using (MemoryStream stream = new MemoryStream(block.Buffer.Length))
                    {
                        using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Compress))
                        {
                            gZipStream.Write(block.Buffer, 0, block.Buffer.Length);
                        }

                        byte[] compressedData = stream.ToArray();
                        var outData = new ByteBlock(block.Id, compressedData, new byte[0]);

                        _writerQueue2.EnqueueForWriting(outData);
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Compression: {ex.Message}"; ;
                _isError = true;
            }
            finally
            {
                Console.WriteLine($"Exit {(int)i}");
                _doneEvents[(int)i].Set();
            }
        }
    }
}
