using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using SLAU.Common.Models;
using SLAU.Common.Network;
using SLAU.Common.Performance;
using System.Diagnostics;

namespace SLAU.Server;
internal class ClientHandler
{
    private readonly DistributedMatrixSolver distributedSolver;
    private readonly LinearMatrixSolver linearSolver;

    public ClientHandler(DistributedMatrixSolver distributedSolver, LinearMatrixSolver linearSolver)
    {
        this.distributedSolver = distributedSolver ?? throw new ArgumentNullException(nameof(distributedSolver));
        this.linearSolver = linearSolver ?? throw new ArgumentNullException(nameof(linearSolver));
    }

    public async Task HandleClientAsync(Socket clientSocket)
    {
        if (clientSocket == null)
            throw new ArgumentNullException(nameof(clientSocket));

        try
        {
            byte[] commandData = await TcpConnection.ReceiveDataAsync(clientSocket);
            var commandJson = Encoding.UTF8.GetString(commandData);
            var command = JsonSerializer.Deserialize<JsonElement>(commandJson);

            if (!command.TryGetProperty("Command", out JsonElement commandElement))
            {
                throw new Exception("Invalid command format: 'Command' property not found");
            }

            string commandType = commandElement.GetString() ?? throw new Exception("Command type is null");

            switch (commandType)
            {
                case "Compare":
                    await HandleComparisonRequest(clientSocket);
                    break;
                case "Solve":
                    await HandleSolveRequest(clientSocket);
                    break;
                default:
                    throw new Exception($"Unknown command: {commandType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            try
            {
                if (clientSocket.Connected)
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                }
                clientSocket.Close();
                Console.WriteLine("Client connection closed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing client connection: {ex.Message}");
            }
        }
    }

    private async Task HandleComparisonRequest(Socket clientSocket)
    {
        Matrix matrix = await ReceiveMatrixAsync(clientSocket);
        var result = new PerformanceResult { MatrixSize = matrix.Size };

        // Линейное решение
        var swLinear = Stopwatch.StartNew();
        result.LinearSolution = linearSolver.Solve(matrix.Clone());
        swLinear.Stop();
        result.LinearTime = swLinear.ElapsedMilliseconds;

        // Распределенное решение
        var swDistributed = Stopwatch.StartNew();
        result.DistributedSolution = await distributedSolver.SolveAsync(matrix.Clone());
        swDistributed.Stop();
        result.DistributedTime = swDistributed.ElapsedMilliseconds;

        result.UpdateDistributedResults(result.DistributedTime, result.DistributedSolution);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string resultJson = JsonSerializer.Serialize(result, options);
        await TcpConnection.SendDataAsync(clientSocket, Encoding.UTF8.GetBytes(resultJson));
    }

    private async Task HandleSolveRequest(Socket clientSocket)
    {
        Matrix matrix = await ReceiveMatrixAsync(clientSocket);
        Console.WriteLine("Starting distributed solution...");
        var result = await distributedSolver.SolveAsync(matrix);
        Console.WriteLine("System solved successfully");

        await SendResultAsync(clientSocket, result);
    }

    private async Task<Matrix> ReceiveMatrixAsync(Socket clientSocket)
    {
        try
        {
            Console.WriteLine("Receiving matrix...");
            byte[] initialSizeData = await TcpConnection.ReceiveDataAsync(clientSocket);
            var matrixSize = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(initialSizeData));

            // Добавляем проверку наличия свойства
            if (!matrixSize.TryGetProperty("Size", out JsonElement sizeElement))
            {
                throw new Exception("Invalid matrix size format: 'Size' property not found");
            }
            int size = sizeElement.GetInt32();

            Console.WriteLine($"Creating matrix of size {size}x{size}");
            Matrix matrix = new Matrix(size);

            while (true)
            {
                byte[] columnData = await TcpConnection.ReceiveDataAsync(clientSocket);
                var columnInfo = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(columnData));

                // Добавляем проверки наличия всех необходимых свойств
                if (!columnInfo.TryGetProperty("StartColumn", out JsonElement startColumnElement) ||
                    !columnInfo.TryGetProperty("ColumnCount", out JsonElement columnCountElement) ||
                    !columnInfo.TryGetProperty("Data", out JsonElement dataElement) ||
                    !columnInfo.TryGetProperty("IsLastBlock", out JsonElement isLastBlockElement))
                {
                    throw new Exception("Invalid column data format: missing required properties");
                }

                int startColumn = startColumnElement.GetInt32();
                int columnCount = columnCountElement.GetInt32();
                bool isLastBlock = isLastBlockElement.GetBoolean();

                var data = dataElement.EnumerateArray()
                                    .Select(x => x.GetDouble())
                                    .ToArray();

                // Проверяем наличие констант только для первого блока
                if (startColumn == 0)
                {
                    if (columnInfo.TryGetProperty("Constants", out JsonElement constantsElement))
                    {
                        var constants = constantsElement.EnumerateArray()
                                                     .Select(x => x.GetDouble())
                                                     .ToArray();
                        for (int i = 0; i < size; i++)
                        {
                            matrix.SetConstant(i, constants[i]);
                        }
                    }
                    else
                    {
                        throw new Exception("Constants not found in first block");
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving matrix: {ex.Message}");
            throw;
        }
    }

    private async Task SendResultAsync(Socket clientSocket, Matrix result)
    {
        var resultSizeInfo = new { Size = result.Size };
        string resultSizeJson = JsonSerializer.Serialize(resultSizeInfo);
        await TcpConnection.SendDataAsync(clientSocket, Encoding.UTF8.GetBytes(resultSizeJson));

        int bufferSize = ServerConfiguration.CalculateBufferSize(result.Size);
        for (int i = 0; i < result.Size; i += bufferSize)
        {
            int rowCount = Math.Min(bufferSize, result.Size - i);
            var rowData = new
            {
                StartRow = i,
                RowCount = rowCount,
                Data = new double[rowCount * result.Size],
                Constants = new double[rowCount],
                IsLastBlock = (i + rowCount >= result.Size)
            };

            for (int r = 0; r < rowCount; r++)
            {
                for (int j = 0; j < result.Size; j++)
                {
                    rowData.Data[r * result.Size + j] = result[i + r, j];
                }
                rowData.Constants[r] = result.GetConstant(i + r);
            }

            string rowJson = JsonSerializer.Serialize(rowData);
            await TcpConnection.SendDataAsync(clientSocket, Encoding.UTF8.GetBytes(rowJson));
        }

        Console.WriteLine("Solution sent to client");
    }
}