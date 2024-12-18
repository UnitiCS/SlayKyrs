using SLAU.Common.Models.Results.Base;

namespace SLAU.Common.Models.Results;

[Serializable]
public class ColumnResult : BaseResult
{
    public double[] ColumnData { get; set; }
    public int ColumnIndex { get; set; }
    public double[] FreeTerms { get; set; }

    public ColumnResult()
    {
    }

    public ColumnResult(int columnIndex, double[] columnData, double[] freeTerms)
    {
        ColumnIndex = columnIndex;
        ColumnData = columnData;
        FreeTerms = freeTerms;
    }
}