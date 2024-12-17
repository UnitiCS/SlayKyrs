using System.Text.Json;
using System.Text.Json.Serialization;

namespace SLAU.Common.Models;

[Serializable]
public class Matrix
{
    [JsonPropertyName("data")]
    private double[] data;

    [JsonPropertyName("constants")]
    private double[] constants;

    [JsonPropertyName("size")]
    public int Size { get; private set; }

    public Matrix(int size)
    {
        if (size <= 0)
            throw new ArgumentException("Matrix size must be positive");

        Size = size;
        data = new double[size * size];
        constants = new double[size];
    }

    public double this[int row, int col]
    {
        get
        {
            ValidateIndices(row, col);
            return data[row * Size + col];
        }
        set
        {
            ValidateIndices(row, col);
            data[row * Size + col] = value;
        }
    }

    public double GetConstant(int index)
    {
        ValidateIndex(index);
        return constants[index];
    }

    public void SetConstant(int index, double value)
    {
        ValidateIndex(index);
        constants[index] = value;
    }

    public Matrix Clone()
    {
        Matrix clone = new Matrix(Size);
        int bufferSize = GetAdaptiveBufferSize();

        // Копируем данные матрицы блоками
        for (int offset = 0; offset < data.Length; offset += bufferSize)
        {
            int count = Math.Min(bufferSize, data.Length - offset);
            Array.Copy(data, offset, clone.data, offset, count);
        }

        // Копируем константы
        Array.Copy(constants, clone.constants, constants.Length);
        return clone;
    }

    public void CopyRow(int sourceRow, int targetRow)
    {
        ValidateIndex(sourceRow);
        ValidateIndex(targetRow);

        int bufferSize = GetAdaptiveBufferSize();
        int offset = 0;
        while (offset < Size)
        {
            int count = Math.Min(bufferSize, Size - offset);
            Array.Copy(data, sourceRow * Size + offset,
                      data, targetRow * Size + offset,
                      count);
            offset += count;
        }
        constants[targetRow] = constants[sourceRow];
    }

    public void GetRowData(int row, double[] buffer, int offset, int count)
    {
        ValidateIndex(row);
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentException("Invalid buffer parameters");
        if (count > Size)
            throw new ArgumentException("Count exceeds matrix size");

        int bufferSize = GetAdaptiveBufferSize();
        int currentOffset = 0;
        while (currentOffset < count)
        {
            int bytesToCopy = Math.Min(bufferSize, count - currentOffset);
            Array.Copy(data, row * Size + currentOffset,
                      buffer, offset + currentOffset,
                      bytesToCopy);
            currentOffset += bytesToCopy;
        }
    }

    public void SetRowData(int row, double[] buffer, int offset, int count)
    {
        ValidateIndex(row);
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentException("Invalid buffer parameters");
        if (count > Size)
            throw new ArgumentException("Count exceeds matrix size");

        int bufferSize = GetAdaptiveBufferSize();
        int currentOffset = 0;
        while (currentOffset < count)
        {
            int bytesToCopy = Math.Min(bufferSize, count - currentOffset);
            Array.Copy(buffer, offset + currentOffset,
                      data, row * Size + currentOffset,
                      bytesToCopy);
            currentOffset += bytesToCopy;
        }
    }

    public double[] GetColumn(int columnIndex)
    {
        ValidateIndex(columnIndex);
        double[] column = new double[Size];
        int bufferSize = GetAdaptiveBufferSize();

        int offset = 0;
        while (offset < Size)
        {
            int count = Math.Min(bufferSize, Size - offset);
            for (int i = offset; i < offset + count; i++)
            {
                column[i] = data[i * Size + columnIndex];
            }
            offset += count;
        }
        return column;
    }

    public void SetColumn(int columnIndex, double[] column)
    {
        ValidateIndex(columnIndex);
        if (column == null || column.Length != Size)
            throw new ArgumentException("Invalid column data");

        int bufferSize = GetAdaptiveBufferSize();
        int offset = 0;
        while (offset < Size)
        {
            int count = Math.Min(bufferSize, Size - offset);
            for (int i = offset; i < offset + count; i++)
            {
                data[i * Size + columnIndex] = column[i];
            }
            offset += count;
        }
    }

    private void ValidateIndices(int row, int col)
    {
        if (row < 0 || row >= Size || col < 0 || col >= Size)
            throw new IndexOutOfRangeException("Matrix indices out of range");
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= Size)
            throw new IndexOutOfRangeException("Index out of range");
    }

    private int GetAdaptiveBufferSize()
    {
        return AdaptiveBuffer.CalculateBufferSize(Size);
    }

    public byte[] Serialize()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            Console.WriteLine($"Serializing matrix {Size}x{Size}");
            Console.WriteLine($"Data array length: {data.Length}");
            Console.WriteLine($"Constants array length: {constants.Length}");

            string jsonString = JsonSerializer.Serialize(this, options);
            byte[] result = System.Text.Encoding.UTF8.GetBytes(jsonString);
            Console.WriteLine($"Serialized to {result.Length} bytes");
            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Serialization error: {ex.Message}");
        }
    }

    public static Matrix Deserialize(byte[] data)
    {
        try
        {
            string jsonString = System.Text.Encoding.UTF8.GetString(data);
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            Matrix result = JsonSerializer.Deserialize<Matrix>(jsonString, options);

            Console.WriteLine($"Deserialized matrix {result.Size}x{result.Size}");
            Console.WriteLine($"Data array length: {result.data.Length}");
            Console.WriteLine($"Constants array length: {result.constants.Length}");

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Deserialization error: {ex.Message}");
        }
    }

    public void PrintMatrix(int maxDisplay = 10)
    {
        Console.WriteLine($"Matrix {Size}x{Size}:");
        int displaySize = Math.Min(Size, maxDisplay);

        for (int i = 0; i < displaySize; i++)
        {
            for (int j = 0; j < displaySize; j++)
            {
                Console.Write($"{this[i, j]:F2}\t");
            }
            Console.Write($"| {constants[i]:F2}");
            if (displaySize < Size)
                Console.Write(" ...");
            Console.WriteLine();
        }

        if (Size > displaySize)
        {
            Console.WriteLine("...");
        }
    }

    public void PrintSolution(int maxDisplay = 20)
    {
        Console.WriteLine("Solution:");
        int displaySize = Math.Min(Size, maxDisplay);

        // Используем адаптивный буфер для вывода решения
        int bufferSize = GetAdaptiveBufferSize();
        for (int offset = 0; offset < displaySize; offset += bufferSize)
        {
            int count = Math.Min(bufferSize, displaySize - offset);
            for (int i = offset; i < offset + count; i++)
            {
                Console.WriteLine($"x{i} = {constants[i]:F6}");
            }
        }

        if (Size > displaySize)
        {
            Console.WriteLine("...");
            Console.WriteLine($"Total {Size} variables");
        }
    }

    // Добавим новые методы для оптимизации работы с большими матрицами

    public void ProcessBlockwise(Action<int, int> action, int blockSize = 0)
    {
        if (blockSize <= 0)
        {
            blockSize = GetAdaptiveBufferSize();
        }

        for (int i = 0; i < Size; i += blockSize)
        {
            int rowCount = Math.Min(blockSize, Size - i);
            for (int j = 0; j < Size; j += blockSize)
            {
                int colCount = Math.Min(blockSize, Size - j);
                action(i, j);
            }
        }
    }

    public void SwapRows(int row1, int row2)
    {
        ValidateIndex(row1);
        ValidateIndex(row2);

        if (row1 == row2) return;

        int bufferSize = GetAdaptiveBufferSize();
        double[] tempBuffer = new double[bufferSize];

        // Обмен элементов матрицы
        for (int offset = 0; offset < Size; offset += bufferSize)
        {
            int count = Math.Min(bufferSize, Size - offset);

            // Сохраняем часть первой строки
            Array.Copy(data, row1 * Size + offset, tempBuffer, 0, count);
            // Копируем часть второй строки в первую
            Array.Copy(data, row2 * Size + offset, data, row1 * Size + offset, count);
            // Копируем сохраненную часть первой строки во вторую
            Array.Copy(tempBuffer, 0, data, row2 * Size + offset, count);
        }

        // Обмен констант
        double tempConstant = constants[row1];
        constants[row1] = constants[row2];
        constants[row2] = tempConstant;
    }

    public void ScaleRow(int row, double factor)
    {
        ValidateIndex(row);

        int bufferSize = GetAdaptiveBufferSize();
        int offset = 0;

        while (offset < Size)
        {
            int count = Math.Min(bufferSize, Size - offset);
            for (int j = 0; j < count; j++)
            {
                data[row * Size + offset + j] *= factor;
            }
            offset += count;
        }
        constants[row] *= factor;
    }

    public double CalculateRowNorm(int row)
    {
        ValidateIndex(row);

        int bufferSize = GetAdaptiveBufferSize();
        double norm = 0.0;
        int offset = 0;

        while (offset < Size)
        {
            int count = Math.Min(bufferSize, Size - offset);
            for (int j = 0; j < count; j++)
            {
                double value = data[row * Size + offset + j];
                norm += value * value;
            }
            offset += count;
        }

        return Math.Sqrt(norm);
    }

    public double[] GetRow(int row)
    {
        ValidateIndex(row);
        double[] rowData = new double[Size];
        Array.Copy(data, row * Size, rowData, 0, Size);
        return rowData;
    }
}