using System.Net;
using System.Net.Sockets;

namespace SLAU.Common.Network;
public static class TcpConnection
{
    private const int MAX_RETRIES = 3;
    private const int RETRY_DELAY_MS = 1000;

    public static Socket CreateListenerSocket(int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Устанавливаем размеры буферов на основе AdaptiveBuffer
        int initialBufferSize = AdaptiveBuffer.CalculateBufferSize(1024); // Начальный размер
        socket.ReceiveBufferSize = initialBufferSize;
        socket.SendBufferSize = initialBufferSize;

        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        socket.Listen(100);
        return socket;
    }

    public static async Task<Socket> ConnectAsync(string host, int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Используем адаптивный размер буфера
        int bufferSize = AdaptiveBuffer.CalculateBufferSize(1024);
        socket.ReceiveBufferSize = bufferSize;
        socket.SendBufferSize = bufferSize;
        socket.ReceiveTimeout = 30000;
        socket.SendTimeout = 30000;

        Exception? lastException = null;
        for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
        {
            try
            {
                await socket.ConnectAsync(host, port);
                return socket;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < MAX_RETRIES - 1)
                {
                    await Task.Delay(RETRY_DELAY_MS);
                }
            }
        }
        throw new Exception($"Failed to connect after {MAX_RETRIES} attempts: {lastException?.Message}");
    }

    public static async Task SendDataAsync(Socket socket, ReadOnlyMemory<byte> data)
    {
        try
        {
            // Отправляем длину данных
            byte[] lengthBytes = BitConverter.GetBytes(data.Length);
            await socket.SendAsync(lengthBytes, SocketFlags.None);

            // Отправляем данные блоками
            int bufferSize = AdaptiveBuffer.CalculateBufferSize(data.Length);
            int offset = 0;

            while (offset < data.Length)
            {
                int remainingBytes = data.Length - offset;
                int bytesToSend = Math.Min(remainingBytes, bufferSize);

                // Используем Memory<byte> для выборки блока данных
                var slice = data.Slice(offset, bytesToSend);

                int bytesSent = await socket.SendAsync(slice, SocketFlags.None);

                if (bytesSent == 0)
                    throw new Exception("Connection closed while sending data");

                offset += bytesSent;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error sending data: {ex.Message}");
        }
    }

    public static async Task<byte[]> ReceiveDataAsync(Socket socket)
    {
        try
        {
            // Получаем длину данных
            byte[] lengthBytes = new byte[4];
            int headerBytesRead = await ReceiveExactAsync(socket, lengthBytes, 4);
            if (headerBytesRead != 4)
                throw new Exception("Failed to receive data length");

            int dataLength = BitConverter.ToInt32(lengthBytes, 0);
            if (dataLength <= 0)
                throw new Exception($"Invalid data length: {dataLength}");

            // Используем адаптивный размер буфера на основе размера получаемых данных
            int bufferSize = AdaptiveBuffer.CalculateBufferSize(dataLength);

            // Получаем данные блоками
            byte[] data = new byte[dataLength];
            int totalBytesRead = 0;

            while (totalBytesRead < dataLength)
            {
                int remainingBytes = dataLength - totalBytesRead;
                int bytesToReceive = Math.Min(remainingBytes, bufferSize);

                int currentBytesRead = await socket.ReceiveAsync(
                    new ArraySegment<byte>(data, totalBytesRead, bytesToReceive),
                    SocketFlags.None);

                if (currentBytesRead == 0)
                    throw new Exception("Connection closed while receiving data");

                totalBytesRead += currentBytesRead;
            }

            return data;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error receiving data: {ex.Message}");
        }
    }

    private static async Task<int> ReceiveExactAsync(Socket socket, byte[] buffer, int count)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < count)
        {
            int bytesRead = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer, totalBytesRead, count - totalBytesRead),
                SocketFlags.None);

            if (bytesRead == 0)
                break;

            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }
}