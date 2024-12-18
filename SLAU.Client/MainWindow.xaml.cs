using Microsoft.Win32;
using SLAU.Common.Logging;
using SLAU.Common.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SLAU.Client;
public partial class MainWindow : Window
{
    private readonly Client _client;
    private readonly ILogger _logger;
    private ObservableCollection<ObservableCollection<string>> _matrixData;
    private ObservableCollection<string> _freeTermsData;

    public MainWindow()
    {
        InitializeComponent();
        _logger = new ConsoleLogger();
        _client = new Client("localhost", 5000, _logger);
        InitializeGrids();
    }

    private void InitializeGrids()
    {
        _matrixData = new ObservableCollection<ObservableCollection<string>>();
        _freeTermsData = new ObservableCollection<string>();
        MatrixGrid.ItemsSource = _matrixData;
        FreeTermsGrid.ItemsSource = _freeTermsData;
    }

    private async void InitNodesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(NodesCountTextBox.Text, out int nodeCount) || nodeCount <= 0)
        {
            MessageBox.Show("Введите корректное количество узлов (больше 0)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            SetControlsEnabled(false);
            await _client.InitializeNodesAsync(nodeCount);
            MessageBox.Show($"Успешно инициализировано {nodeCount} узлов", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при инициализации узлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MatrixSizeTextBox.Text, out int size) || size <= 0 || size > 50000)
        {
            MessageBox.Show("Введите корректный размер матрицы (1-50000)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            SetControlsEnabled(false);
            GenerateRandomMatrix(size);
            UpdateMatrixDisplay();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при генерации матрицы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void SolveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateMatrixData())
            return;

        try
        {
            SetControlsEnabled(false);
            var matrix = CreateMatrixFromGrids();

            ResultTextBox.Text = "Решение...";
            StatsTextBox.Text = "Вычисление...";

            var (solution, stats) = await _client.SolveAsync(matrix);
            DisplayResults(solution, stats);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при решении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            ResultTextBox.Text = "Ошибка вычисления";
            StatsTextBox.Text = ex.Message;
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Matrix files (*.mtx)|*.mtx|All files (*.*)|*.*",
            Title = "Загрузить матрицу"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                SetControlsEnabled(false);
                await LoadMatrixFromFile(dialog.FileName);
                UpdateMatrixDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateMatrixData())
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "Matrix files (*.mtx)|*.mtx|All files (*.*)|*.*",
            Title = "Сохранить матрицу"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                SetControlsEnabled(false);
                await SaveMatrixToFile(dialog.FileName);
                MessageBox.Show("Матрица успешно сохранена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _matrixData.Clear();
            _freeTermsData.Clear();
            ResultTextBox.Clear();
            StatsTextBox.Clear();
            MatrixGrid.Columns.Clear();
            FreeTermsGrid.Columns.Clear();
            UpdateMatrixDisplay();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при очистке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GenerateRandomMatrix(int size)
    {
        var random = new Random();
        _matrixData.Clear();
        _freeTermsData.Clear();

        // Генерация матрицы с контролируемыми значениями
        for (int i = 0; i < size; i++)
        {
            var row = new ObservableCollection<string>();
            double diagonalElement = random.NextDouble() * 10 + 10; // Диагональные элементы от 10 до 20

            for (int j = 0; j < size; j++)
            {
                if (i == j)
                {
                    row.Add(diagonalElement.ToString("F2")); // Диагональный элемент
                }
                else
                {
                    double value = random.NextDouble() * 5; // Недиагональные элементы от 0 до 5
                    row.Add(value.ToString("F2"));
                }
            }
            _matrixData.Add(row);

            // Генерация свободных членов
            double freeTerm = random.NextDouble() * 100; // Свободные члены от 0 до 100
            _freeTermsData.Add(freeTerm.ToString("F2"));
        }

        // Обновляем размеры столбцов в MatrixGrid
        MatrixGrid.Columns.Clear();
        for (int i = 0; i < size; i++)
        {
            var column = new DataGridTextColumn
            {
                Header = $"X{i + 1}",
                Binding = new System.Windows.Data.Binding($"[{i}]"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            MatrixGrid.Columns.Add(column);
        }

        // Обновляем столбец свободных членов
        FreeTermsGrid.Columns.Clear();
        FreeTermsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "B",
            Binding = new System.Windows.Data.Binding(),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        UpdateMatrixDisplay();
    }

    private void UpdateMatrixDisplay()
    {
        // Обновляем высоту строк
        MatrixGrid.RowHeight = 25;
        FreeTermsGrid.RowHeight = 25;

        // Добавляем номера строк
        MatrixGrid.RowHeaderWidth = 50;
        FreeTermsGrid.RowHeaderWidth = 50;

        for (int i = 0; i < _matrixData.Count; i++)
        {
            if (MatrixGrid.Items.Count > i)
            {
                var row = MatrixGrid.Items[i];
                if (row != null)
                {
                    MatrixGrid.RowHeaderTemplate = new DataTemplate();
                    var headerText = new FrameworkElementFactory(typeof(TextBlock));
                    headerText.SetValue(TextBlock.TextProperty, (i + 1).ToString());
                    headerText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                    headerText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    MatrixGrid.RowHeaderTemplate.VisualTree = headerText;
                }
            }
        }
    }

    private void DisplayResults(double[] solution, string stats)
    {
        var resultBuilder = new StringBuilder("Решение:\n");
        for (int i = 0; i < solution.Length; i++)
        {
            resultBuilder.AppendLine($"x{i + 1} = {solution[i]:F6}");
        }
        ResultTextBox.Text = resultBuilder.ToString();
        StatsTextBox.Text = stats;
    }

    private Matrix CreateMatrixFromGrids()
    {
        int size = _matrixData.Count;
        var matrix = new Matrix(size, size);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                if (!double.TryParse(_matrixData[i][j], NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                {
                    throw new FormatException($"Некорректное значение в ячейке [{i},{j}]");
                }
                matrix[i, j] = value;
            }

            if (!double.TryParse(_freeTermsData[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double freeTerm))
            {
                throw new FormatException($"Некорректное значение свободного члена в строке {i}");
            }
            matrix.SetFreeTerm(i, freeTerm);
        }

        return matrix;
    }

    private async Task LoadMatrixFromFile(string filename)
    {
        var lines = await File.ReadAllLinesAsync(filename);
        if (!int.TryParse(lines[0], out int size))
        {
            throw new FormatException("Некорректный формат файла");
        }

        _matrixData.Clear();
        _freeTermsData.Clear();

        for (int i = 0; i < size; i++)
        {
            var values = lines[i + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var row = new ObservableCollection<string>(values.Take(size));
            _matrixData.Add(row);
            _freeTermsData.Add(values[size]);
        }
    }

    private async Task SaveMatrixToFile(string filename)
    {
        using var writer = new StreamWriter(filename);
        await writer.WriteLineAsync(_matrixData.Count.ToString());

        for (int i = 0; i < _matrixData.Count; i++)
        {
            var row = string.Join(" ", _matrixData[i]);
            await writer.WriteLineAsync($"{row} {_freeTermsData[i]}");
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        InitNodesButton.IsEnabled = enabled;
        NodesCountTextBox.IsEnabled = enabled;
        GenerateButton.IsEnabled = enabled;
        LoadButton.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
        SolveButton.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
        MatrixGrid.IsEnabled = enabled;
        FreeTermsGrid.IsEnabled = enabled;
        MatrixSizeTextBox.IsEnabled = enabled;
    }

    private bool ValidateMatrixData()
    {
        if (_matrixData.Count == 0 || _freeTermsData.Count == 0)
        {
            MessageBox.Show("Матрица пуста", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (_matrixData.Count != _freeTermsData.Count)
        {
            MessageBox.Show("Количество строк матрицы не совпадает с количеством свободных членов",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        foreach (var row in _matrixData)
        {
            if (row.Count != _matrixData.Count)
            {
                MessageBox.Show("Матрица не является квадратной",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            foreach (var cell in row)
            {
                if (!double.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    MessageBox.Show("Матрица содержит некорректные значения",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
        }

        foreach (var term in _freeTermsData)
        {
            if (!double.TryParse(term, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                MessageBox.Show("Вектор свободных членов содержит некорректные значения",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        return true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _client.Dispose();
        base.OnClosing(e);
    }
}