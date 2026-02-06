using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public class LabelTextComposer
    {
        private readonly Typeface _typeface;
        private readonly double _fontSize;
        private readonly double _labelWidthPx;
        // optional character limits to force wrapping by character count as well as measured width
        private const int MaxCharsPerLine = 30; // limit characters per line (can be tuned)

        public LabelTextComposer(double labelWidthPx, double fontSize = 9.0, string fontFamily = "Segoe UI")
        {
            _fontSize = fontSize > 0 ? fontSize : 9.0;
            _labelWidthPx = labelWidthPx;
            _typeface = new Typeface(
                new FontFamily(string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily),
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);
        }

        public string[] Compose(
            string medicineName,
            string potency,
            string dose,
            string time,
            string shopName)
        {
            // Normalize inputs
            medicineName = Normalize(medicineName);
            potency = Normalize(potency);
            dose = Normalize(dose);
            time = Normalize(time);
            shopName = Normalize(shopName);

            // Step 1: Wrap medicine into max 2 lines. Respect both measured width and a max character count.
            var medicineLines = WrapText(medicineName, maxLines: 2);
            // Enforce character-based fallback: if any line exceeds MaxCharsPerLine, force split
            for (int i = 0; i < medicineLines.Count; i++)
            {
                if (medicineLines[i].Length > MaxCharsPerLine)
                {
                    var split = ForceSplitByChars(medicineLines[i], MaxCharsPerLine);
                    // replace current line and insert remainder if space allows
                    medicineLines[i] = split.Item1;
                    if (!string.IsNullOrEmpty(split.Item2))
                    {
                        if (medicineLines.Count >= 2)
                        {
                            // push second line to ensure max 2 lines
                            medicineLines[1] = (medicineLines[1] + " " + split.Item2).Trim();
                        }
                        else
                        {
                            medicineLines.Add(split.Item2);
                        }
                    }
                }
            }

            // Ensure exactly 2 medicine lines (so potency is placed on the second line)
            while (medicineLines.Count < 2)
                medicineLines.Add(string.Empty);

            // Step 2: Merge potency into the SECOND medicine line (index 1)
            if (!string.IsNullOrWhiteSpace(potency))
            {
                medicineLines[1] = $"{medicineLines[1]} {potency}".Trim();
            }

            // rely on measured wrapping to split medicine name into at most two lines

            // Step 3: Build final fixed 5-line label
            return new[]
            {
                medicineLines[0], // Line 1
                medicineLines[1], // Line 2 + potency
                dose,              // Line 3
                time,              // Line 4
                shopName           // Line 5
            };
        }

        // -------------------------------
        // HELPERS
        // -------------------------------

        private string Normalize(string input)
        {
            return string.IsNullOrWhiteSpace(input)
                ? string.Empty
                : input.Trim().ToUpperInvariant();
        }

        private List<string> WrapText(string text, int maxLines)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;

            foreach (var word in words)
            {
                // try to append the word to the current line
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;

                // enforce both measured width and character limit
                if (Measure(testLine) <= _labelWidthPx && testLine.Length <= MaxCharsPerLine)
                {
                    currentLine = testLine;
                }
                else
                {
                    // If currentLine is empty it means a single word exceeds either width or char limit;
                    // try to split the word by character limit first, otherwise place it on its own line.
                    if (string.IsNullOrEmpty(currentLine))
                    {
                        if (word.Length > MaxCharsPerLine)
                        {
                            var split = ForceSplitByChars(word, MaxCharsPerLine);
                            result.Add(split.Item1);
                            // remainder becomes the new currentLine (may be further split on next iterations)
                            currentLine = split.Item2;
                        }
                        else
                        {
                            result.Add(word);
                        }
                    }
                    else
                    {
                        result.Add(currentLine);
                        currentLine = word;
                    }

                    if (result.Count == maxLines - 1)
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentLine)
                && result.Count < maxLines)
            {
                result.Add(currentLine);
            }

            return result;
        }

        // Force split a long line by character count into two parts (first part length <= maxChars)
        private (string, string) ForceSplitByChars(string input, int maxChars)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxChars) return (input, string.Empty);

            // try to split at last space before maxChars, otherwise hard split
            int splitPos = input.LastIndexOf(' ', Math.Min(input.Length - 1, maxChars));
            if (splitPos <= 0)
                splitPos = maxChars;

            var first = input.Substring(0, splitPos).Trim();
            var second = input.Substring(splitPos).Trim();
            return (first, second);
        }

        private double Measure(string text)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

            return ft.Width;
        }

        // Public measurement helper that allows callers to measure a text using a specific font size.
        public double MeasureTextWidth(string text, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0.0;

            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _typeface,
                fontSize > 0 ? fontSize : _fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

            return ft.Width;
        }

        // Convenience overload using the composer default font size.
        public double MeasureTextWidth(string text)
            => MeasureTextWidth(text, _fontSize);

        
    }
}
