using SLAU.Common.Enums;
using System.Text.Json.Serialization;

namespace SLAU.Common.Models.Commands.Base;
[JsonDerivedType(typeof(InitNodesCommand), typeDiscriminator: 0)]
[JsonDerivedType(typeof(ColumnInitCommand), typeDiscriminator: 1)]
[JsonDerivedType(typeof(ColumnCommand), typeDiscriminator: 2)]
[JsonDerivedType(typeof(SwapCommand), typeDiscriminator: 3)]
[JsonDerivedType(typeof(EliminationCommand), typeDiscriminator: 4)]
[JsonDerivedType(typeof(SyncCommand), typeDiscriminator: 5)]
[JsonDerivedType(typeof(ElementCommand), typeDiscriminator: 6)]
[JsonDerivedType(typeof(CommandComplete), typeDiscriminator: 7)]
[JsonDerivedType(typeof(MatrixCommand), typeDiscriminator: 8)]
public abstract class CommandBase
{
    public CommandType Type { get; set; }
    public int NodeId { get; set; }
    public Guid CommandId { get; set; }

    protected CommandBase(CommandType type)
    {
        Type = type;
        CommandId = Guid.NewGuid();
    }
}