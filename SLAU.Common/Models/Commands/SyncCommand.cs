using SLAU.Common.Enums;
using SLAU.Common.Models.Commands.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class SyncCommand : CommandBase
{
    public int Stage { get; set; }
    public bool IsComplete { get; set; }

    public SyncCommand() : base(CommandType.Sync)
    {
    }

    public SyncCommand(int stage, bool isComplete = false) : base(CommandType.Sync)
    {
        Stage = stage;
        IsComplete = isComplete;
    }
}