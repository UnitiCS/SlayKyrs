namespace SLAU.Common.Models;
public class RowCommand
{
    public int Command { get; set; }
    public int MatrixSize { get; set; }
    public int PivotRow { get; set; }
    public double PivotValue { get; set; }
    public double PivotConstant { get; set; }  // Добавляем обратно
    public int[] AssignedColumns { get; set; }
    public int[] ColumnIndices { get; set; }   // Оставляем для обратной совместимости
    public double[] PivotColumn { get; set; }
    public double[] ColumnData { get; set; }
    public double[] Constants { get; set; }
}

public class RowResult
{
    public int Command { get; set; }
    public int[] ProcessedColumns { get; set; }   // Обработанные столбцы
    public double[] ColumnData { get; set; }      // Обновленные данные столбцов
    public double[] Constants { get; set; }       // Обновленные константы
}