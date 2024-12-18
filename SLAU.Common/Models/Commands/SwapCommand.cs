using SLAU.Common.Enums;
using SLAU.Common.Models.Commands.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class SwapCommand : CommandBase
{
    public int Row1 { get; set; }
    public int Row2 { get; set; }

    public SwapCommand() : base(CommandType.Swap)
    {
    }

    public SwapCommand(int row1, int row2) : base(CommandType.Swap)
    {
        Row1 = row1;
        Row2 = row2;
    }
}