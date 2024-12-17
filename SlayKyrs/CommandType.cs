using System;

namespace SlayKyrs
{
    public enum CommandType
    {
        ProcessColumn = 1,
        ColumnProcessed = 2,
        Shutdown = 3
    }

    public class ColumnCommand
    {
        public int Command { get; set; }
        public int MatrixSize { get; set; }
        public int ColumnIndex { get; set; }
        public int PivotRow { get; set; }
        public double[] Column { get; set; }
    }

    public class ColumnResult
    {
        public int Command { get; set; }
        public int ColumnIndex { get; set; }
        public double[] Column { get; set; }
    }
}