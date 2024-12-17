using System.Text.Json;
using System.Text;
using SLAU.Common.Models;
using SLAU.Common.Network;
using SLAU.Common.Enums;
using System.Net.Sockets;

namespace SLAU.Server;
public class DistributedMatrixSolver
{
    private readonly List<ComputeNodeInfo> computeNodes;

    public DistributedMatrixSolver(List<ComputeNodeInfo> computeNodes)
    {
        this.computeNodes = computeNodes;
    }

    public async Task<Matrix> SolveAsync(Matrix matrix)
    {
        int n = matrix.Size;
        var activeNodes = computeNodes.Where(node => node.Socket != null && node.Socket.Connected).ToList();
        int nodeCount = activeNodes.Count;

        if (!activeNodes.Any())
            throw new Exception("No compute nodes available");

        Console.WriteLine($"\nStarting distributed solution with {activeNodes.Count} nodes");
        Console.WriteLine($"Matrix size: {n}x{n}");

        try
        {
            // Масштабирование матрицы
            double[] rowScales = new double[n];
            for (int i = 0; i < n; i++)
            {
                double maxInRow = 0;
                for (int j = 0; j < n; j++)
                {
                    maxInRow = Math.Max(maxInRow, Math.Abs(matrix[i, j]));
                }
                if (maxInRow > 0)
                {
                    rowScales[i] = 1.0 / maxInRow;
                    for (int j = 0; j < n; j++)
                    {
                        matrix[i, j] *= rowScales[i];
                    }
                    matrix.SetConstant(i, matrix.GetConstant(i) * rowScales[i]);
                }
            }

            // Распределяем столбцы циклически между узлами
            var nodeColumns = new Dictionary<int, List<int>>();
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                nodeColumns[nodeIndex] = new List<int>();
                // Циклическое распределение столбцов
                for (int col = nodeIndex; col < n; col += nodeCount)
                {
                    nodeColumns[nodeIndex].Add(col);
                }
                Console.WriteLine($"Node {nodeIndex} assigned columns: {string.Join(", ", nodeColumns[nodeIndex])}");
            }

            // Прямой ход метода Гаусса
            for (int k = 0; k < n - 1; k++)
            {
                // Находим ведущий элемент
                await ProcessPivotElement(matrix, k, rowScales);

                var tasks = new List<Task>();
                foreach (var node in activeNodes)
                {
                    var nodeIndex = activeNodes.IndexOf(node);
                    var columns = nodeColumns[nodeIndex];

                    // Отправляем только те столбцы, которые нужны для текущего шага
                    var relevantColumns = columns.Where(col => col >= k).ToList();
                    if (relevantColumns.Any())
                    {
                        tasks.Add(ProcessColumnsOnNodeAsync(node, matrix, k, relevantColumns));
                    }
                }
                await Task.WhenAll(tasks);
            }

            // Обратный ход
            BackSubstitution(matrix);

            // Восстановление масштаба
            for (int i = 0; i < n; i++)
            {
                if (rowScales[i] > 0)
                {
                    matrix.SetConstant(i, matrix.GetConstant(i) / rowScales[i]);
                }
            }

            return matrix;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in distributed solver: {ex.Message}");
            throw;
        }
    }

    private async Task ProcessColumnsOnNodeAsync(ComputeNodeInfo node, Matrix matrix, int pivotRow, List<int> columns)
    {
        try
        {
            Console.WriteLine($"Sending columns {string.Join(", ", columns)} to node on port {node.Port}");

            // Подготавливаем данные столбцов
            double[] columnData = new double[columns.Count * matrix.Size];
            for (int i = 0; i < columns.Count; i++)
            {
                int col = columns[i];
                for (int row = 0; row < matrix.Size; row++)
                {
                    columnData[i * matrix.Size + row] = matrix[row, col];
                }
            }

            var command = new RowCommand
            {
                Command = (int)CommandType.ProcessRows,
                MatrixSize = matrix.Size,
                PivotRow = pivotRow,
                PivotValue = matrix[pivotRow, pivotRow],
                PivotConstant = matrix.GetConstant(pivotRow),
                ColumnIndices = columns.ToArray(),
                PivotColumn = GetColumn(matrix, pivotRow), // Получаем ведущий столбец
                ColumnData = columnData,
                Constants = GetConstants(matrix)
            };

            string jsonCommand = JsonSerializer.Serialize(command);
            await TcpConnection.SendDataAsync(node.Socket, Encoding.UTF8.GetBytes(jsonCommand));

            // Получаем результат
            byte[] resultBytes = await TcpConnection.ReceiveDataAsync(node.Socket);
            string jsonResult = Encoding.UTF8.GetString(resultBytes);
            var result = JsonSerializer.Deserialize<RowResult>(jsonResult);

            // Обновляем столбцы в матрице
            for (int i = 0; i < columns.Count; i++)
            {
                int col = columns[i];
                for (int row = pivotRow + 1; row < matrix.Size; row++)
                {
                    matrix[row, col] = result.ColumnData[i * matrix.Size + row];
                }
            }

            // Обновляем константы если это последний столбец
            if (columns.Contains(matrix.Size - 1))
            {
                for (int row = pivotRow + 1; row < matrix.Size; row++)
                {
                    matrix.SetConstant(row, result.Constants[row]);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing columns on node {node.Port}: {ex.Message}");
            throw;
        }
    }

    private double[] GetColumn(Matrix matrix, int colIndex)
    {
        double[] column = new double[matrix.Size];
        for (int i = 0; i < matrix.Size; i++)
        {
            column[i] = matrix[i, colIndex];
        }
        return column;
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

    private Dictionary<int, List<int>> DistributeColumns(int matrixSize, int nodeCount)
    {
        var nodeColumns = new Dictionary<int, List<int>>();
        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            nodeColumns[nodeIndex] = new List<int>();
            for (int col = nodeIndex; col < matrixSize; col += nodeCount)
            {
                nodeColumns[nodeIndex].Add(col);
            }
            Console.WriteLine($"Node {nodeIndex} assigned columns: {string.Join(", ", nodeColumns[nodeIndex].Take(5))}...");
        }
        return nodeColumns;
    }

    private async Task ProcessPivotElement(Matrix matrix, int k, double[] rowScales)
    {
        int maxRow = k;
        double maxVal = Math.Abs(matrix[k, k]);

        for (int i = k + 1; i < matrix.Size; i++)
        {
            double absVal = Math.Abs(matrix[i, k]);
            if (absVal > maxVal)
            {
                maxVal = absVal;
                maxRow = i;
            }
        }

        if (maxRow != k)
        {
            matrix.SwapRows(k, maxRow);
            double tempScale = rowScales[k];
            rowScales[k] = rowScales[maxRow];
            rowScales[maxRow] = tempScale;
        }

        if (Math.Abs(matrix[k, k]) < 1e-12)
        {
            throw new Exception($"Matrix is numerically singular at step {k}");
        }
    }

    private void BackSubstitution(Matrix matrix)
    {
        for (int i = matrix.Size - 1; i >= 0; i--)
        {
            double sum = 0;
            double c = 0;
            for (int j = i + 1; j < matrix.Size; j++)
            {
                // Компенсированное суммирование для уменьшения ошибок округления
                double y = matrix[i, j] * matrix.GetConstant(j) - c;
                double t = sum + y;
                c = (t - sum) - y;
                sum = t;
            }
            matrix.SetConstant(i, (matrix.GetConstant(i) - sum) / matrix[i, i]);
        }
    }

    private void ValidateColumnDistribution(Dictionary<int, List<int>> nodeColumns, int matrixSize)
    {
        var allColumns = new HashSet<int>();
        foreach (var columns in nodeColumns.Values)
        {
            foreach (var col in columns)
            {
                if (!allColumns.Add(col))
                {
                    throw new Exception($"Column {col} is assigned to multiple nodes");
                }
            }
        }

        for (int i = 0; i < matrixSize; i++)
        {
            if (!allColumns.Contains(i))
            {
                throw new Exception($"Column {i} is not assigned to any node");
            }
        }
    }

    private async Task SynchronizeNodesAsync(List<ComputeNodeInfo> activeNodes)
    {
        var syncTasks = new List<Task>();
        foreach (var node in activeNodes)
        {
            var command = new RowCommand
            {
                Command = (int)CommandType.Synchronize
            };

            string jsonCommand = JsonSerializer.Serialize(command);
            syncTasks.Add(TcpConnection.SendDataAsync(node.Socket, Encoding.UTF8.GetBytes(jsonCommand)));
        }
        await Task.WhenAll(syncTasks);

        // Ожидаем подтверждения от всех узлов
        foreach (var node in activeNodes)
        {
            await TcpConnection.ReceiveDataAsync(node.Socket);
        }
    }

    private async Task SendColumnDataAsync(Socket socket, Matrix matrix, int startCol, int colCount)
    {
        var columnData = new
        {
            StartColumn = startCol,
            ColumnCount = colCount,
            Data = new double[colCount * matrix.Size],
            IsLastBlock = (startCol + colCount >= matrix.Size)
        };

        // Упаковываем данные столбцов
        for (int c = 0; c < colCount; c++)
        {
            int currentCol = startCol + c;
            for (int i = 0; i < matrix.Size; i++)
            {
                columnData.Data[c * matrix.Size + i] = matrix[i, currentCol];
            }
        }

        string jsonData = JsonSerializer.Serialize(columnData);
        await TcpConnection.SendDataAsync(socket, Encoding.UTF8.GetBytes(jsonData));
    }

    private async Task<double[]> ReceiveColumnDataAsync(Socket socket, int matrixSize)
    {
        byte[] data = await TcpConnection.ReceiveDataAsync(socket);
        string jsonData = Encoding.UTF8.GetString(data);
        var columnInfo = JsonSerializer.Deserialize<JsonElement>(jsonData);

        return columnInfo.GetProperty("Data").EnumerateArray()
                        .Select(x => x.GetDouble())
                        .ToArray();
    }

    private void UpdateMatrixColumns(Matrix matrix, double[] columnData, int startCol, int colCount)
    {
        for (int c = 0; c < colCount; c++)
        {
            int currentCol = startCol + c;
            for (int i = 0; i < matrix.Size; i++)
            {
                matrix[i, currentCol] = columnData[c * matrix.Size + i];
            }
        }
    }
}