using System.Net.Sockets;

/// <summary>
/// Класс для хранения информации о вычислительном узле:
/// - Сокет подключения
/// - Порт узла
/// </summary>
namespace SLAU.Server;
public class ComputeNodeInfo
{
    public int Port { get; set; }
    public Socket Socket { get; set; }
}