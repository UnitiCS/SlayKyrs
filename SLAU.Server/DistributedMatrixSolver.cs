using SLAU.Common.Logging;
using SLAU.Common.Models;
using SLAU.Common.Models.Commands;
using SLAU.Common.Models.Results;
using SLAU.Common.Models.Results.Base;
using SLAU.Common.Performance;
using SLAU.Server.Configuration;

namespace SLAU.Server;
public class DistributedMatrixSolver
{
    private readonly NodeManager _nodeManager;
    private readonly ILogger _logger;
    private readonly PerformanceMonitor _performanceMonitor;

    public DistributedMatrixSolver(NodeManager nodeManager, ILogger logger, PerformanceMonitor performanceMonitor)
    {
        _nodeManager = nodeManager ?? throw new ArgumentNullException(nameof(nodeManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
    }

    public async Task<double[]> SolveAsync(Matrix matrix)
    {
        try
        {
            _performanceMonitor.StartMeasurement("distribution");
            var nodes = _nodeManager.GetAvailableNodes();
            var columnsPerNode = DistributeColumns(matrix.Columns, nodes.Count);

            // Инициализация узлов
            await InitializeNodesAsync(matrix, nodes, columnsPerNode);
            _performanceMonitor.StopMeasurement("distribution");

            // Прямой ход метода Гаусса
            for (int i = 0; i < matrix.Rows; i++)
            {
                _performanceMonitor.StartMeasurement($"elimination_step_{i}");

                // Поиск ведущего элемента
                var pivot = await FindPivotElementAsync(i, nodes);
                if (Math.Abs(pivot.Value) < 1e-10)
                {
                    throw new InvalidOperationException("Matrix is singular");
                }

                // Обмен строк, если необходимо
                if (pivot.Row != i)
                {
                    await BroadcastSwapCommandAsync(nodes, i, pivot.Row);
                }

                // Исключение переменных
                await EliminateVariablesAsync(i, nodes);

                _performanceMonitor.StopMeasurement($"elimination_step_{i}");
            }

            // Обратный ход
            _performanceMonitor.StartMeasurement("back_substitution");
            var result = await BackSubstitutionAsync(matrix.Rows, nodes);
            _performanceMonitor.StopMeasurement("back_substitution");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error solving matrix: {ex.Message}");
            throw;
        }
    }

    private int[] DistributeColumns(int totalColumns, int nodeCount)
    {
        var columnsPerNode = new int[nodeCount];
        int baseColumns = totalColumns / nodeCount;
        int remainder = totalColumns % nodeCount;

        for (int i = 0; i < nodeCount; i++)
        {
            columnsPerNode[i] = baseColumns + (i < remainder ? 1 : 0);
        }

        return columnsPerNode;
    }

    private async Task InitializeNodesAsync(Matrix matrix, IList<ComputeNodeInfo> nodes, int[] columnsPerNode)
    {
        var tasks = new List<Task>();
        int currentColumn = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var columns = columnsPerNode[i];

            var columnData = new List<double[]>();
            for (int j = 0; j < columns; j++)
            {
                columnData.Add(matrix.GetColumn(currentColumn + j));
            }

            var command = new ColumnInitCommand
            {
                NodeId = node.Id,
                ColumnIndex = currentColumn,
                ColumnData = columnData.SelectMany(c => c).ToArray(),
                FreeTerms = Enumerable.Range(0, matrix.Rows).Select(r => matrix.GetFreeTerm(r)).ToArray()
            };

            tasks.Add(_nodeManager.SendToNodeAsync<ColumnResult>(node.Id, command));
            currentColumn += columns;
        }

        await Task.WhenAll(tasks);
    }

    private async Task<ElementResult> FindPivotElementAsync(int row, IList<ComputeNodeInfo> nodes)
    {
        var tasks = nodes.Select(node =>
            _nodeManager.SendToNodeAsync<ElementResult>(
                node.Id,
                new ElementCommand { Row = row }));

        var results = await Task.WhenAll(tasks);
        return results.OrderByDescending(r => Math.Abs(r.Value)).First();
    }

    private async Task BroadcastSwapCommandAsync(IList<ComputeNodeInfo> nodes, int row1, int row2)
    {
        var command = new SwapCommand(row1, row2);
        var tasks = nodes.Select(node =>
            _nodeManager.SendToNodeAsync<BaseResult>(node.Id, command));
        await Task.WhenAll(tasks);
    }

    private async Task EliminateVariablesAsync(int pivotRow, IList<ComputeNodeInfo> nodes)
    {
        var tasks = new List<Task>();
        foreach (var node in nodes)
        {
            var command = new EliminationCommand
            {
                NodeId = node.Id,
                PivotRow = pivotRow
            };
            tasks.Add(_nodeManager.SendToNodeAsync<BaseResult>(node.Id, command));
        }
        await Task.WhenAll(tasks);
    }

    private async Task<double[]> BackSubstitutionAsync(int size, IList<ComputeNodeInfo> nodes)
    {
        var result = new double[size];
        for (int i = size - 1; i >= 0; i--)
        {
            var tasks = nodes.Select(node =>
                _nodeManager.SendToNodeAsync<ElementResult>(
                    node.Id,
                    new ElementCommand { Row = i, Column = i }));

            var elements = await Task.WhenAll(tasks);
            var value = elements.Sum(e => e.Value);
            result[i] = value;

            if (i > 0)
            {
                await BroadcastSolutionAsync(nodes, i, value);
            }
        }
        return result;
    }

    private async Task BroadcastSolutionAsync(IList<ComputeNodeInfo> nodes, int row, double value)
    {
        var command = new ElementCommand { Row = row, Value = value };
        var tasks = nodes.Select(node =>
            _nodeManager.SendToNodeAsync<BaseResult>(node.Id, command));
        await Task.WhenAll(tasks);
    }
}