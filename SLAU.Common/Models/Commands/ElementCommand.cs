using SLAU.Common.Enums;
using SLAU.Common.Models.Commands.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class ElementCommand : CommandBase
{
    public int Row { get; set; }
    public int Column { get; set; }
    public double Value { get; set; }

    public ElementCommand() : base(CommandType.Element)
    {
    }

    public ElementCommand(int row, int column, double value)
        : base(CommandType.Element)
    {
        Row = row;
        Column = column;
        Value = value;
    }
}