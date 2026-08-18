namespace Delima.Core.Roster;

/// <summary>
/// Grid sizing calculation for class cards per Normal_SSO §4.3 and Technical Architecture §5.
/// </summary>
public static class GridCalculator
{
    public const int FixedRows = 5;
    public const int ConstantCardHeightPx = 99;
    public const int DefaultUsableWidthPx = 1318;
    public const int DefaultUsableHeightPx = 536;
    public const int DefaultGapPx = 10;

    public sealed record GridDimensions(
        int Columns,
        int Rows,
        int CardWidthPx,
        int CardHeightPx,
        int ApproximateCharsPerLine
    );

    public static GridDimensions Calculate(
        int pupilCount,
        int usableWidthPx = DefaultUsableWidthPx,
        int usableHeightPx = DefaultUsableHeightPx,
        int gapPx = DefaultGapPx)
    {
        int totalItems = pupilCount + 1; // + 1 for "Nama saya tiada" card
        int cols = (int)Math.Ceiling((double)totalItems / FixedRows);
        if (cols < 7) cols = 7; // minimum 7 columns for 30-34 class layout

        int cardW = (usableWidthPx - (cols - 1) * gapPx) / cols;
        int cardH = (usableHeightPx - (FixedRows - 1) * gapPx) / FixedRows;

        int charsPerLine = cols switch
        {
            <= 7 => 19,
            8 => 16,
            _ => 14
        };

        return new GridDimensions(
            Columns: cols,
            Rows: FixedRows,
            CardWidthPx: cardW,
            CardHeightPx: cardH,
            ApproximateCharsPerLine: charsPerLine
        );
    }
}
