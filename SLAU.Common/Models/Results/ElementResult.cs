using SLAU.Common.Models.Results.Base;

namespace SLAU.Common.Models.Results;

[Serializable]
public class ElementResult : BaseResult
{
    public double Value { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }

    public ElementResult()
    {
    }

    public ElementResult(int row, int column, double value)
    {
        Row = row;
        Column = column;
        Value = value;
    }
}