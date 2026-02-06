namespace HomeoMahanagarLabelCleanV2.Helpers
{
    /// <summary>
    /// Centralized printing and export constants plus unit conversion helpers.
    ///
    /// Rationale:
    /// - Keep a single source of truth for physical label dimensions and default padding.
    /// - Provide deterministic conversions between millimeters, WPF DIPs (device-independent
    ///   pixels) and PDF/print points so layout math is consistent across Preview, PDF and Print.
    /// </summary>
    public static class PrintConstants
    {
        /// <summary>
        /// Physical label width in millimeters. All layout and export code must use this value
        /// (or parameters derived from it) so that on-screen preview, PDF export and printer
        /// layouts share the same physical dimensions.
        /// </summary>
        public const double LabelWidthMm = 50.0;

        /// <summary>
        /// Physical label height in millimeters.
        /// </summary>
        public const double LabelHeightMm = 30.0;

        /// <summary>
        /// Inner padding from label edge in millimeters (default 2 mm).
        /// This value is used both for visual preview padding and to compute printer GAP/Insets
        /// so printed output avoids the label edge and any sensor area.
        /// </summary>
        public const double LabelPaddingMm = 2.0;

        /// <summary>
        /// Inner padding converted to WPF DIPs (Device Independent Pixels, 1/96 inch).
        ///
        /// Why store DIP padding: WPF layout and measurement occur in DIPs. Computing a DIP
        /// equivalent of the physical padding ensures Preview rendering uses the exact same
        /// inner gap as printing/export math which operates in physical units.
        /// </summary>
        public static double LabelPaddingDip => MmToDip(LabelPaddingMm);

        // --------------------------- UNIT CONVERSIONS ---------------------------
        // The conversions below are used throughout the codebase when moving between
        // physical units (mm), WPF DIPs (for Measure/Arrange/UI rendering) and points
        // (used by some PDF APIs). Keeping these formulas centralized avoids off-by-one
        // DPI/layout mismatches when rendering to different targets.

        /// <summary>
        /// Convert millimeters to WPF device-independent pixels (DIPs).
        /// Formula: mm / 25.4 * 96.0
        ///
        /// Usage note: After calling Measure/Arrange on a WPF element with a size in DIPs,
        /// the visual layout will match the printed size when the same conversion is used
        /// to rasterize to an image at the target DPI (see PdfHelper.RenderElementToPngBytes).
        /// </summary>
        /// <param name="mm">Length in millimeters.</param>
        /// <returns>Equivalent length in DIPs.</returns>
        public static double MmToDip(double mm) => mm / 25.4 * 96.0;

        /// <summary>
        /// Convert device-independent pixels (DIPs) to PDF/print points (1 point = 1/72 inch).
        /// Formula: dip * 72 / 96.
        ///
        /// This is used by PDF/vector export code where APIs expect point units.
        /// </summary>
        /// <param name="dip">Length in DIPs.</param>
        /// <returns>Length in points.</returns>
        public static double DipToPoints(double dip) => dip * 72.0 / 96.0;

        /// <summary>
        /// Convert millimeters to PDF/print points by first converting to DIPs then to points.
        /// This preserves the single-source conversion pipeline and reduces rounding differences.
        /// </summary>
        /// <param name="mm">Length in millimeters.</param>
        /// <returns>Length in points.</returns>
        public static double MmToPoints(double mm) => MmToDip(mm) * 72.0 / 96.0;
    }
}
