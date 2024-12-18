using SLAU.Common.Models.Commands.Base;
using SLAU.Common.Enums;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class MatrixCommand : CommandBase
{
    public List<double[]> Columns { get; set; }
    public double[] FreeTerms { get; set; }
    public int Rows { get; set; }

    public MatrixCommand() : base(CommandType.Matrix)
    {
    }

    public MatrixCommand(Matrix matrix) : base(CommandType.Matrix)
    {
        Columns = matrix.SerializableColumns;
        FreeTerms = matrix.SerializableFreeTerms;
        Rows = matrix.Rows;
    }

    public Matrix ToMatrix()
    {
        var matrix = new Matrix(Rows, Columns.Count);
        for (int j = 0; j < Columns.Count; j++)
        {
            matrix.SetColumn(j, Columns[j]);
        }
        for (int i = 0; i < FreeTerms.Length; i++)
        {
            matrix.SetFreeTerm(i, FreeTerms[i]);
        }
        return matrix;
    }
}