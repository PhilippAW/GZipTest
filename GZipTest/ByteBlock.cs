namespace GZipTest
{
    public class ByteBlock
    {
        public ByteBlock(int id, byte[] buffer, byte[] compressedBuffer)
        {
            Id = id;
            Buffer = buffer;
            CompressedBuffer = compressedBuffer;
        }

        public int Id { get; private set; }

        public byte[] Buffer { get; set; }

        public byte[] CompressedBuffer { get; set; }

    }
}
