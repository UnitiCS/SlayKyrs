using SLAU.Common.Enums;
using SLAU.Common.Models.Commands.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class InitNodesCommand : CommandBase
{
    public int NodeCount { get; set; }

    public InitNodesCommand() : base(CommandType.InitNodes)
    {
    }
}