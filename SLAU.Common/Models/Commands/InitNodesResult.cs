using SLAU.Common.Models.Results.Base;

namespace SLAU.Common.Models.Commands;

[Serializable]
public class InitNodesResult : BaseResult
{
    public int ActiveNodeCount { get; set; }
}