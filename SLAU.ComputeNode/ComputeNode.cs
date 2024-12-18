using SLAU.Common.Logging;
using SLAU.Common.Models;
using SLAU.Common.Models.Commands;
using SLAU.Common.Models.Commands.Base;
using SLAU.Common.Models.Results;
using SLAU.Common.Models.Results.Base;
using SLAU.Common.Network;
using System.Net.Sockets;

namespace SLAU.ComputeNode;
public class ComputeNode : IDisposable
{
    private readonly TcpClient _client;
    private readonly ILogger _logger;
    private readonly TcpConnection _connection;
    private Matrix _localMatrix;
    private int _startColumnIndex;
    private int _columnCount;
    private bool _isDisposed;

    public ComputeNode(TcpClient client, ILogger logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connection = new TcpConnection(client, logger);
    }

    public async Task ProcessRequestsAsync()
    {
        try
        {
            while (_client.Connected)
            {
                var command = await _connection.ReceiveAsync<CommandBase>();
                var result = await ProcessCommandAsync(command);
                await _connection.SendAsync(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing requests: {ex.Message}");
        }
    }

    private async Task<BaseResult> ProcessCommandAsync(CommandBase command)
    {
        try
        {
            switch (command)
            {
                case ColumnInitCommand init:
                    return await InitializeColumnsAsync(init);
                case ColumnCommand col:
                    return await ProcessColumnAsync(col);
                case SwapCommand swap:
                    return await ProcessSwapAsync(swap);
                case EliminationCommand elim:
                    return await ProcessEliminationAsync(elim);
                case ElementCommand elem:
                    return await ProcessElementAsync(elem);
                default:
                    throw new InvalidOperationException($"Unknown command type: {command.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing command {command.GetType().Name}: {ex.Message}");
            return new BaseResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<ColumnResult> InitializeColumnsAsync(ColumnInitCommand command)
    {
        return await Task.Run(() =>
        {
            _startColumnIndex = command.ColumnIndex;
            _columnCount = command.ColumnData.Length / command.FreeTerms.Length;

            _localMatrix = new Matrix(command.FreeTerms.Length, _columnCount);

            // Заполнение локальной матрицы данными
            for (int i = 0; i < command.FreeTerms.Length; i++)
            {
                for (int j = 0; j < _columnCount; j++)
                {
                    _localMatrix[i, j] = command.ColumnData[i * _columnCount + j];
                }
                _localMatrix.SetFreeTerm(i, command.FreeTerms[i]);
            }

            return new ColumnResult
            {
                IsSuccess = true,
                ColumnIndex = _startColumnIndex,
                ColumnData = command.ColumnData,
                FreeTerms = command.FreeTerms
            };
        });
    }

    private async Task<ColumnResult> ProcessColumnAsync(ColumnCommand command)
    {
        return await Task.Run(() =>
        {
            var result = new ColumnResult
            {
                ColumnIndex = command.ColumnIndex,
                ColumnData = new double[_localMatrix.Rows],
                FreeTerms = new double[_localMatrix.Rows]
            };

            for (int i = 0; i < _localMatrix.Rows; i++)
            {
                result.ColumnData[i] = _localMatrix[i, command.ColumnIndex - _startColumnIndex];
                result.FreeTerms[i] = _localMatrix.GetFreeTerm(i);
            }

            return result;
        });
    }

    private async Task<BaseResult> ProcessSwapAsync(SwapCommand command)
    {
        return await Task.Run(() =>
        {
            _localMatrix.SwapRows(command.Row1, command.Row2);
            return new BaseResult { IsSuccess = true };
        });
    }

    private async Task<BaseResult> ProcessEliminationAsync(EliminationCommand command)
    {
        return await Task.Run(() =>
        {
            int pivotRow = command.PivotRow;
            double pivotElement = _localMatrix[pivotRow, 0];

            for (int i = 0; i < _localMatrix.Rows; i++)
            {
                if (i != pivotRow)
                {
                    double factor = _localMatrix[i, 0] / pivotElement;
                    for (int j = 0; j < _localMatrix.Columns; j++)
                    {
                        _localMatrix[i, j] -= factor * _localMatrix[pivotRow, j];
                    }
                    _localMatrix.SetFreeTerm(i, _localMatrix.GetFreeTerm(i) -
                        factor * _localMatrix.GetFreeTerm(pivotRow));
                }
            }

            return new BaseResult { IsSuccess = true };
        });
    }

    private async Task<ElementResult> ProcessElementAsync(ElementCommand command)
    {
        return await Task.Run(() =>
        {
            int localColumn = command.Column - _startColumnIndex;
            if (localColumn >= 0 && localColumn < _columnCount)
            {
                return new ElementResult
                {
                    Row = command.Row,
                    Column = command.Column,
                    Value = _localMatrix[command.Row, localColumn]
                };
            }

            return new ElementResult
            {
                Row = command.Row,
                Column = command.Column,
                Value = 0
            };
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _connection?.Dispose();
        _client?.Dispose();
        _isDisposed = true;
    }
}