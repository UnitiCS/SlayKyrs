using System.Text.Json;
using System.Text;
using SLAU.Common.Models;
using SLAU.Common.Network;
using SLAU.Common;
using SLAU.Common.Performance;
using System.Net.Sockets;

namespace SLAU.Client;
public class Client
{
    private readonly string serverHost;
    private readonly int serverPort;

    public Client(string serverHost, int serverPort)
    {
        this.serverHost = serverHost;
        this.serverPort = serverPort;
    }

    public async Task<Matrix> SolveSystemAsync(Matrix matrix)
    {
        using var socket = await TcpConnection.ConnectAsync(serverHost, serverPort);

        try
        {
            Console.WriteLine("Connecting to server...");

            // Отправляем команду
            var command = new { Command = "Solve", Size = matrix.Size };
            string commandJson = JsonSerializer.Serialize(command);
            await TcpConnection.SendDataAsync(socket, Encoding.UTF8.GetBytes(commandJson));

            // Отправляем матрицу по столбцам
            await SendMatrixByColumnsAsync(socket, matrix);
            Console.WriteLine("Matrix transfer completed. Waiting for solution...");

            // Получаем результат по столбцам
            Matrix result = await ReceiveMatrixByColumnsAsync(socket);
            Console.WriteLine("Solution received successfully");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in client: {ex.Message}");
            throw;
        }
    }

    public async Task<PerformanceResult> CompareMethodsAsync(Matrix matrix)
    {
        using var socket = await TcpConnection.ConnectAsync(serverHost, serverPort);
        try
        {
            Console.WriteLine("\n=== Starting Performance Comparison ===");

            // Отправляем команду для сравнения методов
            var command = new { Command = "Compare", Size = matrix.Size };
            string commandJson = JsonSerializer.Serialize(command);
            await TcpConnection.SendDataAsync(socket, Encoding.UTF8.GetBytes(commandJson));

            // Отправляем матрицу по столбцам
            await SendMatrixByColumnsAsync(socket, matrix);

            // Получаем результаты сравнения
            byte[] resultData = await TcpConnection.ReceiveDataAsync(socket);
            string resultJson = Encoding.UTF8.GetString(resultData);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var result = JsonSerializer.Deserialize<PerformanceResult>(resultJson, options);

            if (result == null)
                throw new Exception("Failed to deserialize performance results");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in comparison: {ex.Message}");
            throw;
        }
    }

    private async Task SendMatrixByColumnsAsync(Socket socket, Matrix matrix)
    {
        // Отправляем размер матрицы
        var sizeInfo = new { Size = matrix.Size };
        string sizeJson = JsonSerializer.Serialize(sizeInfo);
        await TcpConnection.SendDataAsync(socket, Encoding.UTF8.GetBytes(sizeJson));

        int bufferSize = AdaptiveBuffer.CalculateBufferSize(matrix.Size);

        // Отправляем столбцы матрицы
        for (int j = 0; j < matrix.Size; j += bufferSize)
        {
            int colCount = Math.Min(bufferSize, matrix.Size - j);
            var columnData = new
            {
                StartColumn = j,
                ColumnCount = colCount,
                Data = new double[colCount * matrix.Size],
                Constants = j == 0 ? GetConstants(matrix) : null, // Константы отправляем только с первым блоком
                IsLastBlock = (j + colCount >= matrix.Size)
            };

            // Заполняем данные столбцов
            for (int c = 0; c < colCount; c++)
            {
                int currentCol = j + c;
                for (int i = 0; i < matrix.Size; i++)
                {
                    columnData.Data[c * matrix.Size + i] = matrix[i, currentCol];
                }
            }

            string columnJson = JsonSerializer.Serialize(columnData);
            await TcpConnection.SendDataAsync(socket, Encoding.UTF8.GetBytes(columnJson));
        }
    }

    private async Task<Matrix> ReceiveMatrixByColumnsAsync(Socket socket)
    {
        // Получаем размер матрицы
        byte[] sizeData = await TcpConnection.ReceiveDataAsync(socket);
        var sizeInfo = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(sizeData));
        int size = sizeInfo.GetProperty("Size").GetInt32();

        Matrix matrix = new Matrix(size);

        // Получаем столбцы
        while (true)
        {
            byte[] columnData = await TcpConnection.ReceiveDataAsync(socket);
            var columnInfo = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(columnData));

            int startColumn = columnInfo.GetProperty("StartColumn").GetInt32();
            int columnCount = columnInfo.GetProperty("ColumnCount").GetInt32();
            bool isLastBlock = columnInfo.GetProperty("IsLastBlock").GetBoolean();

            var data = columnInfo.GetProperty("Data").EnumerateArray()
                               .Select(x => x.GetDouble())
                               .ToArray();

            // Получаем константы с первым блоком
            if (startColumn == 0 && columnInfo.TryGetProperty("Constants", out JsonElement constantsElement))
            {
                var constants = constantsElement.EnumerateArray()
                                             .Select(x => x.GetDouble())
                                             .ToArray();
                for (int i = 0; i < size; i++)
                {
                    matrix.SetConstant(i, constants[i]);
                }
            }

            // Заполняем столбцы матрицы
            for (int c = 0; c < columnCount; c++)
            {
                int currentCol = startColumn + c;
                for (int i = 0; i < size; i++)
                {
                    matrix[i, currentCol] = data[c * size + i];
                }
            }

            if (isLastBlock) break;
        }

        return matrix;
    }

    private double[] GetConstants(Matrix matrix)
    {
        double[] constants = new double[matrix.Size];
        for (int i = 0; i < matrix.Size; i++)
        {
            constants[i] = matrix.GetConstant(i);
        }
        return constants;
    }

    public static Matrix GenerateRandomMatrix(int size)
    {
        if (size <= 0)
            throw new ArgumentException("Matrix size must be positive");

        Random random = new Random();
        Matrix matrix = new Matrix(size);
        int bufferSize = AdaptiveBuffer.CalculateBufferSize(size);

        // Генерируем матрицу по столбцам
        for (int j = 0; j < size; j += bufferSize)
        {
            int colCount = Math.Min(bufferSize, size - j);
            for (int c = 0; c < colCount; c++)
            {
                int currentCol = j + c;
                for (int i = 0; i < size; i++)
                {
                    if (i != currentCol)
                    {
                        matrix[i, currentCol] = random.Next(-5, 6);
                    }
                }
            }
        }

        // Обеспечиваем диагональное преобладание
        for (int i = 0; i < size; i++)
        {
            double rowSum = 0;
            for (int j = 0; j < size; j++)
            {
                if (i != j)
                {
                    rowSum += Math.Abs(matrix[i, j]);
                }
            }
            matrix[i, i] = rowSum + random.Next(1, 10); // Диагональный элемент больше суммы модулей остальных элементов строки
        }

        // Генерируем вектор свободных членов
        for (int i = 0; i < size; i++)
        {
            matrix.SetConstant(i, random.Next(-50, 51));
        }

        return matrix;
    }

    private void ValidateMatrix(Matrix matrix)
    {
        if (matrix == null)
            throw new ArgumentNullException(nameof(matrix));

        if (matrix.Size <= 0)
            throw new ArgumentException("Matrix size must be positive");

        if (matrix.Size > 50000)
            throw new ArgumentException("Matrix size exceeds maximum allowed (50000)");

        // Проверка на нулевые диагональные элементы
        for (int i = 0; i < matrix.Size; i++)
        {
            if (Math.Abs(matrix[i, i]) < 1e-10)
            {
                throw new ArgumentException($"Zero or near-zero diagonal element at position {i}");
            }
        }
    }

    private async Task<bool> CheckServerConnectionAsync()
    {
        try
        {
            using var testSocket = await TcpConnection.ConnectAsync(serverHost, serverPort);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int CalculateOptimalBufferSize(int matrixSize)
    {
        // Определяем оптимальный размер буфера в зависимости от размера матрицы
        const int maxBufferSize = 1024 * 1024; // 1 MB
        const int minBufferSize = 1024; // 1 KB

        int optimalSize = Math.Min(maxBufferSize,
                                 Math.Max(minBufferSize,
                                        matrixSize * sizeof(double)));

        // Округляем до ближайшей степени двойки
        int power = (int)Math.Ceiling(Math.Log2(optimalSize));
        return 1 << power;
    }

    private async Task SendProgressUpdate(Socket socket, int progress)
    {
        try
        {
            var progressInfo = new { Progress = progress };
            string progressJson = JsonSerializer.Serialize(progressInfo);
            await TcpConnection.SendDataAsync(socket, Encoding.UTF8.GetBytes(progressJson));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending progress update: {ex.Message}");
        }
    }

    private void LogTransferProgress(int currentColumn, int totalColumns)
    {
        int progressPercentage = (int)((double)currentColumn / totalColumns * 100);
        if (progressPercentage % 10 == 0) // Логируем каждые 10%
        {
            Console.WriteLine($"Matrix transfer progress: {progressPercentage}%");
        }
    }

    private async Task<bool> ValidateServerResponse(Socket socket)
    {
        try
        {
            byte[] response = await TcpConnection.ReceiveDataAsync(socket);
            var responseObj = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(response));
            return responseObj.GetProperty("Status").GetString() == "OK";
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnection()
    {
        try
        {
            using var socket = await TcpConnection.ConnectAsync(serverHost, serverPort);
            var testCommand = new { Command = "Test" };
            string commandJson = JsonSerializer.Serialize(testCommand);
            await TcpConnection.SendDataAsync(socket, Encoding.UTF8.GetBytes(commandJson));
            return await ValidateServerResponse(socket);
        }
        catch
        {
            return false;
        }
    }
}