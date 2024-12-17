using System.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using SLAU.Common.Models;
using System.Windows.Threading;
using SLAU.Common.Performance;

namespace SLAU.Client;
public partial class MainWindow : Window
{
    private Client client;
    private Matrix currentMatrix;
    private Matrix solution;
    private bool isSolving;
    private ObservableCollection<MatrixRow> matrixData;
    private ObservableCollection<SolutionRow> solutionData;
    private readonly DispatcherTimer statusTimer;

    public MainWindow()
    {
        InitializeComponent();

        matrixData = new ObservableCollection<MatrixRow>();
        solutionData = new ObservableCollection<SolutionRow>();
        MatrixGrid.ItemsSource = matrixData;
        SolutionGrid.ItemsSource = solutionData;

        // Инициализация таймера для обновления статуса
        statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        statusTimer.Tick += StatusTimer_Tick;

        UpdateUIState();
    }

    private async void Solve_Click(object sender, RoutedEventArgs e)
    {
        if (isSolving)
        {
            MessageBox.Show("Solution is already in progress!", "Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (currentMatrix == null)
        {
            MessageBox.Show("Please generate a matrix first!", "Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            isSolving = true;
            UpdateUIState();
            ProgressBar.IsIndeterminate = true;
            statusTimer.Start();

            LogMessage("Starting solution process...");

            string host = HostTextBox.Text;
            if (!int.TryParse(PortTextBox.Text, out int port))
            {
                throw new ArgumentException("Invalid port number");
            }

            client = new Client(host, port);
            LogMessage($"Connecting to server at {host}:{port}...");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            solution = await Task.Run(() => client.SolveSystemAsync(currentMatrix));
            sw.Stop();

            LogMessage($"Solution completed in {sw.ElapsedMilliseconds}ms");

            DisplaySolution();

            MessageBox.Show("Solution completed successfully!",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogMessage($"Error: {ex.Message}");
            MessageBox.Show($"Error occurred: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            isSolving = false;
            ProgressBar.IsIndeterminate = false;
            statusTimer.Stop();
            UpdateUIState();
        }
    }

    private void GenerateMatrix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(MatrixSizeTextBox.Text, out int size))
            {
                MessageBox.Show("Please enter a valid matrix size!", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (size <= 0 || size > 50000)
            {
                MessageBox.Show("Matrix size must be between 1 and 50000!", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LogMessage($"Generating random matrix of size {size}x{size}...");
            currentMatrix = Client.GenerateRandomMatrix(size);
            DisplayMatrix();
            LogMessage("Matrix generated successfully.");

            // Явно вызываем обновление состояния UI
            UpdateUIState();
        }
        catch (Exception ex)
        {
            LogMessage($"Error generating matrix: {ex.Message}");
            MessageBox.Show($"Error generating matrix: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (currentMatrix == null)
        {
            MessageBox.Show("Please generate a matrix first!", "Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            CompareButton.IsEnabled = false;
            ProgressBar.IsIndeterminate = true;

            LogMessage("\n=== Starting Performance Comparison ===");
            LogMessage($"Matrix size: {currentMatrix.Size}x{currentMatrix.Size}");

            var result = await client.CompareMethodsAsync(currentMatrix);

            LogMessage("\n=== Detailed Results ===");
            LogMessage($"Matrix size: {result.MatrixSize}x{result.MatrixSize}");
            LogMessage($"Linear method time: {result.LinearTime}ms");
            LogMessage($"Distributed method time: {result.DistributedTime}ms");

            if (result.Speedup > 1)
            {
                LogMessage($"Speedup achieved: {result.Speedup:F2}x");
                LogMessage($"Performance improvement: {((result.Speedup - 1) * 100):F1}%");
                LogMessage($"Time saved: {result.LinearTime - result.DistributedTime}ms");
            }
            else
            {
                LogMessage($"Performance decrease: {(1 / result.Speedup):F2}x slower");
                LogMessage($"Additional overhead: {result.DistributedTime - result.LinearTime}ms");
            }

            LogMessage($"\nSolution accuracy:");
            LogMessage($"Solutions match: {result.SolutionsMatch}");
            LogMessage($"Maximum error: {result.MaxError:E6}");

            if (result.ErrorDistribution.Any())
            {
                LogMessage("\nError distribution:");
                var percentiles = new[] { 50, 90, 95, 99 };
                var sortedErrors = result.ErrorDistribution.Values.OrderBy(x => x).ToList();
                foreach (var p in percentiles)
                {
                    int index = (int)(p / 100.0 * (sortedErrors.Count - 1));
                    LogMessage($"{p}th percentile: {sortedErrors[index]:E6}");
                }
            }

            MessageBox.Show($"Comparison completed!\n" +
                           $"Linear time: {result.LinearTime}ms\n" +
                           $"Distributed time: {result.DistributedTime}ms\n" +
                           $"Speedup: {result.Speedup:F2}x\n" +
                           $"Solutions match: {result.SolutionsMatch}",
                "Comparison Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogMessage($"Error during comparison: {ex.Message}");
            MessageBox.Show($"Error during comparison: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CompareButton.IsEnabled = true;
            ProgressBar.IsIndeterminate = false;
        }
    }
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        currentMatrix = null;
        solution = null;
        matrixData.Clear();
        solutionData.Clear();
        LogTextBox.Clear();
        ProgressBar.Value = 0;
        UpdateUIState();
    }

    private void DisplayMatrix()
    {
        matrixData.Clear();
        if (currentMatrix == null) return;

        // Ограничиваем отображение для больших матриц
        int displaySize = Math.Min(currentMatrix.Size, 20);
        for (int i = 0; i < displaySize; i++)
        {
            var row = new MatrixRow { Index = i };
            for (int j = 0; j < displaySize; j++)
            {
                row.Values.Add(currentMatrix[i, j]);
            }
            row.Constant = currentMatrix.GetConstant(i);
            matrixData.Add(row);
        }

        if (currentMatrix.Size > displaySize)
        {
            LogMessage($"Note: Displaying only first {displaySize}x{displaySize} elements of {currentMatrix.Size}x{currentMatrix.Size} matrix");
        }
    }

    private void DisplaySolution()
    {
        solutionData.Clear();
        if (solution == null) return;

        // Добавляем все решения в таблицу
        for (int i = 0; i < solution.Size; i++)
        {
            solutionData.Add(new SolutionRow
            {
                Index = i + 1,
                Value = solution.GetConstant(i)
            });
        }

        LogMessage("\nSolution values (first 10):");
        for (int i = 0; i < Math.Min(10, solution.Size); i++)
        {
            LogMessage($"x[{i + 1}] = {solution.GetConstant(i):F6}");
        }
    }

    private void LogMessage(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        LogTextBox.AppendText($"[{timestamp}] {message}\n");
        LogTextBox.ScrollToEnd();
    }

    private void UpdateUIState()
    {
        bool canInteract = !isSolving;
        HostTextBox.IsEnabled = canInteract;
        PortTextBox.IsEnabled = canInteract;
        MatrixSizeTextBox.IsEnabled = canInteract;
        // Изменим условие для кнопки Solve
        SolveButton.IsEnabled = canInteract && currentMatrix != null;
        GenerateMatrix.IsEnabled = canInteract;
        ClearButton.IsEnabled = canInteract;

        // Добавим отладочный вывод
        Console.WriteLine($"UpdateUIState: isSolving={isSolving}, currentMatrix is {(currentMatrix == null ? "null" : "not null")}");
        Console.WriteLine($"SolveButton.IsEnabled = {SolveButton.IsEnabled}");
    }

    private void StatusTimer_Tick(object sender, EventArgs e)
    {
        if (!isSolving)
        {
            ProgressBar.Value = 0;
            return;
        }

        ProgressBar.Value = (ProgressBar.Value + 1) % 100;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (isSolving)
        {
            var result = MessageBox.Show(
                "Solution is in progress. Are you sure you want to exit?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        statusTimer.Stop();
        base.OnClosing(e);
    }
}

public class MatrixRow
{
    public int Index { get; set; }
    public ObservableCollection<double> Values { get; set; } = new ObservableCollection<double>();
    public double Constant { get; set; }
}

public class SolutionRow
{
    public int Index { get; set; }
    public double Value { get; set; }
}