namespace HomeoMahanagarLabelCleanV2.Helpers
{
    // Centralize printing/export constants and unit conversions
    public static class PrintConstants
    {
        // physical label size in millimeters (single source of truth)
        public const double LabelWidthMm = 50.0;
        public const double LabelHeightMm = 30.0;

        // inner padding from label edge in millimeters (use 2 mm by default)
        public const double LabelPaddingMm = 2.0;

        // inner padding used in preview (DIPs) computed from millimeters so all code paths
        // using PrintConstants.LabelPaddingDip will get an exact 2 mm equivalent in device-independent pixels.
        public static double LabelPaddingDip => MmToDip(LabelPaddingMm);

        // conversions
        public static double MmToDip(double mm) => mm / 25.4 * 96.0;
        public static double DipToPoints(double dip) => dip * 72.0 / 96.0;
        public static double MmToPoints(double mm) => MmToDip(mm) * 72.0 / 96.0;
    }
}