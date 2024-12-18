using SLAU.Common.Enums;
using SLAU.Common.Models.Commands.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class ColumnInitCommand : CommandBase
{
    public double[] ColumnData { get; set; }
    public int ColumnIndex { get; set; }
    public double[] FreeTerms { get; set; }

    public ColumnInitCommand() : base(CommandType.ColumnInit)
    {
    }

    public ColumnInitCommand(int columnIndex, double[] columnData, double[] freeTerms)
        : base(CommandType.ColumnInit)
    {
        ColumnIndex = columnIndex;
        ColumnData = columnData;
        FreeTerms = freeTerms;
    }
}