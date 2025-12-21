namespace ImageCompressorApp.Models;
public struct CompressProgressStatus
{
    public int Current { get; init; }
    public int Total { get; init; }
    public int Left => Math.Max(Total - Current, 0);
    public double Percent
    {
        get
        {
            return Total > 0 ? (double)((double)Current / Total) : 0.00d;
        }
    }
    public CompressProgressStatus(int current, int total)
    {
        Current = current;
        Total = total;
    }
}
