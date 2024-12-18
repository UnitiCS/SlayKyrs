using SLAU.Common.Models.Commands.Base;
using SLAU.Common.Enums;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class CommandComplete : CommandBase
{
    public CommandComplete() : base(CommandType.Complete)
    {
    }
}