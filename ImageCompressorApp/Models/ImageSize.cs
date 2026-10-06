namespace ImageCompressorApp.Models;
public record struct ImageSize(int Width, int Height)
{
    public override readonly string ToString() => $"{Width}x{Height}";
}