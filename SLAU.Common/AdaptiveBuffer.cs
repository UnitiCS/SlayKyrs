namespace SLAU.Common;
public class AdaptiveBuffer
{
    private byte[] _buffer;
    private const int InitialSize = 1024;
    private const int MaxSize = 1024 * 1024 * 100; // 100 MB

    public byte[] Buffer => _buffer;

    public AdaptiveBuffer()
    {
        _buffer = new byte[InitialSize];
    }

    public void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
            return;

        int newSize = _buffer.Length;
        while (newSize < required)
        {
            newSize *= 2;
            if (newSize > MaxSize)
                throw new System.InvalidOperationException($"Required buffer size {required} exceeds maximum allowed size of {MaxSize} bytes");
        }

        byte[] newBuffer = new byte[newSize];
        System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
        _buffer = newBuffer;
    }

    public void Reset()
    {
        if (_buffer.Length > InitialSize)
        {
            _buffer = new byte[InitialSize];
        }
    }
}