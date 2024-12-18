using SLAU.Common.Enums;
using SLAU.Common.Models.Commands.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class EliminationCommand : CommandBase
{
    public int PivotRow { get; set; }
    public int CurrentRow { get; set; }
    public double Multiplier { get; set; }

    public EliminationCommand() : base(CommandType.Elimination)
    {
    }

    public EliminationCommand(int pivotRow, int currentRow, double multiplier)
        : base(CommandType.Elimination)
    {
        PivotRow = pivotRow;
        CurrentRow = currentRow;
        Multiplier = multiplier;
    }
}