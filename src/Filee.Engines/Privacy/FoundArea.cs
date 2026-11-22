// Morph change: a part of a picture the face and text finders found, in parts of the picture (0 to 1), as the Hide
// window's frames are.

namespace Filee.Engines.Privacy;

public readonly record struct FoundArea(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>Grown by <paramref name="by"/> of its size on each side (a face's hair and chin), kept on the picture.</summary>
    public FoundArea Grow(double by)
    {
        var x = Math.Max(0, X - Width * by);
        var y = Math.Max(0, Y - Height * by);
        return new(x, y, Math.Min(1, Right + Width * by) - x, Math.Min(1, Bottom + Height * by) - y);
    }

    /// <summary>How much two areas overlap: their shared part against both together (0 to 1).</summary>
    public double Overlap(FoundArea other)
    {
        var w = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        var h = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        if (w <= 0 || h <= 0)
            return 0;
        var shared = w * h;
        return shared / (Width * Height + other.Width * other.Height - shared);
    }
}
