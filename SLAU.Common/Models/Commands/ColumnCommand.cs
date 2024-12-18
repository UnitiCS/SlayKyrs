using SLAU.Common.Enums;
using SLAU.Common.Models.Commands.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class ColumnCommand : CommandBase
{
    public double[] ColumnData { get; set; }
    public int ColumnIndex { get; set; }
    public int CurrentRow { get; set; }

    public ColumnCommand() : base(CommandType.Column)
    {
    }

    public ColumnCommand(int columnIndex, double[] columnData, int currentRow)
        : base(CommandType.Column)
    {
        ColumnIndex = columnIndex;
        ColumnData = columnData;
        CurrentRow = currentRow;
    }
}