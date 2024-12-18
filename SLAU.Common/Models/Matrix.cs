using System.Text;
using System.Text.Json.Serialization;

namespace SLAU.Common.Models;

[Serializable]
public class Matrix
{
    private double[,] _data;
    private double[] _freeTerms;

    [JsonIgnore]
    public int Rows { get; private set; }
    [JsonIgnore]
    public int Columns { get; private set; }

    // Свойства для сериализации
    [JsonPropertyName("columns")]
    public List<double[]> SerializableColumns
    {
        get
        {
            var columns = new List<double[]>();
            for (int j = 0; j < Columns; j++)
            {
                columns.Add(GetColumn(j));
            }
            return columns;
        }
        set
        {
            if (value != null)
            {
                Columns = value.Count;
                Rows = value[0].Length;
                _data = new double[Rows, Columns];
                for (int j = 0; j < Columns; j++)
                {
                    SetColumn(j, value[j]);
                }
            }
        }
    }

    [JsonPropertyName("freeTerms")]
    public double[] SerializableFreeTerms
    {
        get => _freeTerms;
        set => _freeTerms = value;
    }

    [JsonConstructor]
    public Matrix() { }

    public Matrix(int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
            throw new ArgumentException("Matrix dimensions must be positive");

        Rows = rows;
        Columns = columns;
        _data = new double[rows, columns];
        _freeTerms = new double[rows];
    }

    public Matrix(double[,] data, double[] freeTerms)
    {
        if (data == null || freeTerms == null)
            throw new ArgumentNullException("Data arrays cannot be null");

        Rows = data.GetLength(0);
        Columns = data.GetLength(1);

        if (freeTerms.Length != Rows)
            throw new ArgumentException("Free terms array length must match matrix rows");

        _data = (double[,])data.Clone();
        _freeTerms = (double[])freeTerms.Clone();
    }

    public double this[int row, int column]
    {
        get
        {
            ValidateIndices(row, column);
            return _data[row, column];
        }
        set
        {
            ValidateIndices(row, column);
            _data[row, column] = value;
        }
    }

    public double GetFreeTerm(int row)
    {
        if (row < 0 || row >= Rows)
            throw new ArgumentOutOfRangeException(nameof(row));
        return _freeTerms[row];
    }

    public void SetFreeTerm(int row, double value)
    {
        if (row < 0 || row >= Rows)
            throw new ArgumentOutOfRangeException(nameof(row));
        _freeTerms[row] = value;
    }

    public double[] GetColumn(int column)
    {
        if (column < 0 || column >= Columns)
            throw new ArgumentOutOfRangeException(nameof(column));

        double[] result = new double[Rows];
        for (int i = 0; i < Rows; i++)
            result[i] = _data[i, column];
        return result;
    }

    public void SetColumn(int column, double[] values)
    {
        if (column < 0 || column >= Columns)
            throw new ArgumentOutOfRangeException(nameof(column));
        if (values.Length != Rows)
            throw new ArgumentException("Values array length must match matrix rows");

        for (int i = 0; i < Rows; i++)
            _data[i, column] = values[i];
    }

    public void SwapRows(int row1, int row2)
    {
        if (row1 < 0 || row1 >= Rows || row2 < 0 || row2 >= Rows)
            throw new ArgumentOutOfRangeException("Invalid row indices");

        for (int j = 0; j < Columns; j++)
        {
            double temp = _data[row1, j];
            _data[row1, j] = _data[row2, j];
            _data[row2, j] = temp;
        }

        double tempTerm = _freeTerms[row1];
        _freeTerms[row1] = _freeTerms[row2];
        _freeTerms[row2] = tempTerm;
    }

    private void ValidateIndices(int row, int column)
    {
        if (row < 0 || row >= Rows)
            throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0 || column >= Columns)
            throw new ArgumentOutOfRangeException(nameof(column));
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                sb.Append($"{_data[i, j]:F2}\t");
            }
            sb.Append($"| {_freeTerms[i]:F2}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}