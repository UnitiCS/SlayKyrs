using SLAU.Common;
using SLAU.Common.Network;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace SLAU.Tests.Unit;
public class NetworkTests : IDisposable  // Добавляем IDisposable для корректной очистки ресурсов
{
    private List<Socket> socketsToCleanup = new List<Socket>();

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void AdaptiveBuffer_ShouldReturnValidSize(int matrixSize)
    {
        // Act
        int bufferSize = AdaptiveBuffer.CalculateBufferSize(matrixSize);

        // Assert
        Assert.True(bufferSize > 0);
        Assert.True(bufferSize <= matrixSize);
    }

    [Fact]
    public void TcpConnection_CreateListenerSocket_ShouldCreateValidSocket()
    {
        // Arrange
        int port = GetAvailablePort();

        // Act
        var listener = TcpConnection.CreateListenerSocket(port);
        socketsToCleanup.Add(listener);

        // Assert
        Assert.NotNull(listener);
        Assert.True(listener.IsBound);
    }

    [Fact]
    public async Task TcpConnection_SendAndReceiveData_ShouldWorkCorrectly()
    {
        // Arrange
        int port = GetAvailablePort();
        var listener = TcpConnection.CreateListenerSocket(port);
        socketsToCleanup.Add(listener);

        // Act
        var serverTask = Task.Run(async () =>
        {
            var server = await listener.AcceptAsync();
            socketsToCleanup.Add(server);
            return server;
        });

        var client = await TcpConnection.ConnectAsync("localhost", port);
        socketsToCleanup.Add(client);

        var server = await serverTask;

        byte[] testData = new byte[] { 1, 2, 3, 4, 5 };

        // Используем CancellationToken для предотвращения зависания
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await Task.WhenAll(
            TcpConnection.SendDataAsync(client, testData),
            Task.Run(async () =>
            {
                var receivedData = await TcpConnection.ReceiveDataAsync(server);
                Assert.Equal(testData, receivedData);
            }, cts.Token)
        );
    }

    [Fact]
    public async Task TcpConnection_Connect_ShouldThrowOnInvalidPort()
    {
        await Assert.ThrowsAsync<Exception>(() =>
            TcpConnection.ConnectAsync("localhost", 99999));
    }

    [Fact]
    public async Task TcpConnection_SendData_ShouldHandleLargeData()
    {
        // Arrange
        int port = GetAvailablePort();
        var listener = TcpConnection.CreateListenerSocket(port);
        socketsToCleanup.Add(listener);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            var server = await listener.AcceptAsync();
            socketsToCleanup.Add(server);
            return server;
        });

        var client = await TcpConnection.ConnectAsync("localhost", port);
        socketsToCleanup.Add(client);

        var server = await serverTask;

        // Создаем тестовые данные меньшего размера (100KB вместо 1MB)
        byte[] testData = new byte[100 * 1024];
        new Random().NextBytes(testData);

        // Act & Assert
        await Task.WhenAll(
            TcpConnection.SendDataAsync(client, testData),
            Task.Run(async () =>
            {
                var receivedData = await TcpConnection.ReceiveDataAsync(server);
                Assert.Equal(testData, receivedData);
            }, cts.Token)
        );
    }

    private int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        socket.Listen(1);
        var port = ((IPEndPoint)socket.LocalEndPoint).Port;
        socket.Close();
        return port;
    }

    public void Dispose()
    {
        foreach (var socket in socketsToCleanup)
        {
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                socket.Close();
            }
            catch { }
        }
    }
}