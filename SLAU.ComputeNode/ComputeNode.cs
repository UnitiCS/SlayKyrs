using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using SLAU.Common.Models;
using SLAU.Common.Network;
using SLAU.Common.Enums;
using System.Collections.Concurrent;

namespace SLAU.ComputeNode;
public class ComputeNode
{
    private Socket socket;
    private readonly int nodePort;
    private bool isRunning;
    private readonly object lockObject = new object();

    public ComputeNode(int nodePort)
    {
        this.nodePort = nodePort;
    }

    public async Task StartAsync()
    {
        try
        {
            Console.WriteLine($"Initializing compute node for port {nodePort}");
            socket = await TcpConnection.ConnectAsync("localhost", nodePort);
            isRunning = true;
            Console.WriteLine($"Connected to server on port {nodePort}");

            while (isRunning)
            {
                try
                {
                    byte[] data = await TcpConnection.ReceiveDataAsync(socket);

                    if (data.Length == 4)
                    {
                        int commandType = BitConverter.ToInt32(data, 0);
                        if (commandType == (int)CommandType.Shutdown)
                        {
                            Console.WriteLine("Received shutdown command");
                            Stop();
                            return;
                        }
                    }

                    string jsonCommand = Encoding.UTF8.GetString(data);
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var command = JsonSerializer.Deserialize<RowCommand>(jsonCommand, options);

                    if (command == null)
                    {
                        throw new Exception("Deserialized command is null");
                    }

                    switch (command.Command)
                    {
                        case (int)CommandType.ProcessRows:
                            Console.WriteLine($"Processing columns: {string.Join(", ", command.ColumnIndices)}");
                            await ProcessColumnsCommand(socket, command);
                            break;

                        case (int)CommandType.Synchronize:
                            await ProcessSynchronizeCommand(socket, command);
                            break;

                        default:
                            throw new Exception($"Unknown command type: {command.Command}");
                    }
                }
                catch (Exception ex) when (!isRunning)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing command: {ex.Message}");
                    if (!socket.Connected)
                    {
                        Console.WriteLine("Lost connection to server");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in compute node: {ex.Message}");
            throw;
        }
        finally
        {
            socket?.Close();
            Console.WriteLine("Compute node stopped");
        }
    }

    private async Task ProcessColumnsCommand(Socket clientSocket, RowCommand command)
    {
        try
        {
            Console.WriteLine($"Processing columns below row {command.PivotRow}");
            Console.WriteLine($"Assigned columns: {string.Join(", ", command.ColumnIndices)}");

            var result = new RowResult
            {
                Command = (int)CommandType.RowsProcessed,
                ProcessedColumns = command.ColumnIndices,
                ColumnData = new double[command.ColumnData.Length],
                Constants = new double[command.MatrixSize]
            };

            // Копируем исходные данные
            Array.Copy(command.ColumnData, result.ColumnData, command.ColumnData.Length);
            Array.Copy(command.Constants, result.Constants, command.Constants.Length);

            // Параллельная обработка назначенных столбцов
            int processorCount = Environment.ProcessorCount;
            int optimalThreadCount = Math.Min(processorCount * 2, command.ColumnIndices.Length);

            var partitioner = Partitioner.Create(0, command.ColumnIndices.Length);

            await Task.Run(() =>
            {
                Parallel.ForEach(partitioner, new ParallelOptions { MaxDegreeOfParallelism = optimalThreadCount },
                    range =>
                    {
                        for (int colIndex = range.Item1; colIndex < range.Item2; colIndex++)
                        {
                            ProcessColumn(colIndex, command, result);
                        }
                    });
            });

            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string jsonResult = JsonSerializer.Serialize(result, options);
            await TcpConnection.SendDataAsync(clientSocket, Encoding.UTF8.GetBytes(jsonResult));

            Console.WriteLine($"Processed columns: {string.Join(", ", command.ColumnIndices)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing columns: {ex.Message}");
            throw;
        }
    }

    private void ProcessColumn(int colIndex, RowCommand command, RowResult result)
    {
        int currentCol = command.ColumnIndices[colIndex];

        // Обрабатываем только столбцы, начиная с ведущего
        if (currentCol >= command.PivotRow)
        {
            // Обрабатываем элементы столбца ниже ведущей строки
            for (int i = command.PivotRow + 1; i < command.MatrixSize; i++)
            {
                // Вычисляем множитель
                double factor = command.PivotColumn[i] / command.PivotValue;

                // Обновляем элемент в столбце
                result.ColumnData[colIndex * command.MatrixSize + i] =
                    command.ColumnData[colIndex * command.MatrixSize + i] -
                    factor * command.ColumnData[colIndex * command.MatrixSize + command.PivotRow];

                // Обновляем константы только если это последний столбец
                if (currentCol == command.MatrixSize - 1)
                {
                    result.Constants[i] = command.Constants[i] - factor * command.PivotConstant;
                }
            }
        }
    }

    private async Task ProcessSynchronizeCommand(Socket clientSocket, RowCommand command)
    {
        try
        {
            var result = new RowResult
            {
                Command = (int)CommandType.Synchronize
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string jsonResult = JsonSerializer.Serialize(result, options);
            await TcpConnection.SendDataAsync(clientSocket, Encoding.UTF8.GetBytes(jsonResult));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing synchronize command: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        lock (lockObject)
        {
            if (!isRunning) return;

            isRunning = false;
            try
            {
                socket?.Close();
                Console.WriteLine("Compute node stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping compute node: {ex.Message}");
            }
        }
    }
}