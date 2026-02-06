# HomeoMahanagarLabelCleanV2

## Project Overview

WPF (.NET 8) application that composes and prints 50 mm × 30 mm medicine labels used in homeopathic clinics. The project enforces pixel- and physical-size parity across three outputs: on-screen Preview, exported PDF, and thermal printer output.

This repository prioritizes correctness and deterministic printing over convenience or loose layout assumptions.

## Key Features

- Compose fixed 5-line labels with automatic wrapping for medicine+potency.
- Exact, DPI-aware raster export to PDF (PNG embedded) and several print delivery paths:
  - TSPL (raw text commands) for supported thermal printers
  - PDF spool
  - Rasterized PrintVisual via FixedDocument
- Runtime tuning for printer DPI and inner label padding (persisted in AppState)
- Diagnostics: TSPL files and exported PDFs written to LocalAppData Diagnostics for debugging

## Architecture Overview

- UI: WPF MVVM — view models drive a small preview UI and commands for Export/Print.
- Rendering: A dedicated `PrintLabelView` constructs the visual representation (code-behind controls) and is used as the single source of truth for rasterization.
- Export/Print: `PdfHelper` rasterizes a WPF visual to a PNG at a configured DPI and embeds it into a PDF. `PrintService` builds TSPL when available or falls back to PDF spool / `PrintVisual`.
- Measurement: `LabelTextComposer` uses WPF `FormattedText` to measure text in DIPs; `PrintService` converts those measurements to printer dots.

## Printing & PDF Pipeline (high level)

1. The admin layout (AdminLayout) contains five `LabelCanvasItem` entries describing logical lines and coordinates.
2. `LabelTextComposer` composes the logical lines (medicine/potency/dose/time/shop) and applies wrapping/auto-shrink rules.
3. `PrintLabelView.RenderItems` places `TextBlock` children at scaled coordinates using `PrintConstants` for padding and size. Width/Height are set to `PrintConstants.MmToDip(...)` before rasterization.
4. For PDF export and printer raster printing:
   - `PdfHelper.RenderElementToPngBytes` performs `Measure` → `Arrange` → `UpdateLayout` and renders the visual to a `RenderTargetBitmap` at a chosen DPI.
   - The PNG is embedded into a PDF page sized to the physical label dimensions so the image maps 1:1 to real-world millimeters.
5. For TSPL-capable printers, `PrintService.BuildTsplBytes` converts WPF-measured widths (DIPs) to printer dots using the configured DPI and emits `TEXT X,Y,...` commands; it also provides an auto-shrink loop for device fonts.
6. The application prefers raster embedding in the PDF to guarantee visual parity between Preview and printed output; TSPL is used when available for direct printer control.

## Important Design Constraints & Critical Invariants

Do not change these casually. Any edits to these areas require calibration and verification on hardware.

- PrintConstants (label size and padding): `PrintConstants.LabelWidthMm`, `LabelHeightMm`, `LabelPaddingMm` — these are the single source of truth for physical dimensions.
- DIPs ↔ dots conversion math and DPI handling:
  - `PrintService.BuildTsplBytes` relies on `dipToDots = printerDpi / 96.0` and round-trip arithmetic.
  - `PdfHelper.RenderElementToPngBytes` uses DIPs -> pixels via the chosen DPI when creating the RenderTargetBitmap.
- Measurement pipeline: `LabelTextComposer.Measure` uses WPF text measurement. If you change font families, sizes, or font metrics you must verify the TSPL centering and auto-shrink behavior.
- Border/hardware sensor assumptions: the code hides decorative borders during export and uses a small safety offset when computing vertical start (`startY`) to avoid sensor/edge artifacts. Changing this logic affects printed alignment.
- `printerWidthScale` and auto-shrink step in `BuildTsplBytes` are calibration heuristics for TSPL device fonts. Adjust only after printing test patches.

## Project Structure

- `/Views` — WPF views and print visual (`PrintLabelView`, preview window)
- `/ViewModels` — MVVM view models used by the UI
- `/Services` — printing, composition and diagnostics logic (`PrintService`, `LabelTextComposer`, `RawPrinterHelper`)
- `/Helpers` — PDF export and unit conversions (`PdfHelper`, `PrintConstants`, `PdfFontResolver`)
- `/Models` — `LabelCanvasItem`, `AppStorage` and domain models
- `/Logging` — simple application logging helpers
- Diagnostics output: `%LOCALAPPDATA%/HomeoMahanagarLabelCleanV2/Diagnostics`

## Build & Run (Windows)

Prerequisites:
- .NET 8 SDK installed
- Visual Studio 2022/2023 (or `dotnet` CLI) with WPF/.NET desktop workload

Build and run with Visual Studio:
1. Open solution in Visual Studio.
2. Build the solution (Debug/Any CPU is fine).
3. Run the application and test Preview → Export PDF → Print.

CLI:
- dotnet build
- dotnet run --project HomeoMahanagarLabelCleanV2.csproj

Notes about running while debugging: the build may fail if the running executable is locked; stop the running app or kill its process before rebuilding.

## Common Pitfalls

- Changing font families, font sizes, or admin-layout coordinates without revalidating on physical printer leads to misaligned prints.
- Editing `PrintConstants` or unit conversion formulas without recalibration will break parity between Preview and Print.
- Modifying TSPL generation (GAP, startY, dipToDots math, or `printerWidthScale`) will change printer behavior; always test on the actual thermal printer.
- Embedded PDF vector fallback is not guaranteed to match raster output; prefer raster (PNG embed) when fidelity matters.

## Quick Calibration Tips

- Use the Preview window tuning controls (DPI and Padding) to save `AppState.Storage.LabelPrinterDpi` and `LabelPaddingDip`.
- Export diagnostics: TSPL text files and exported PDFs are written to the Diagnostics folder. Print these to a test roll and adjust DPI/padding until alignment is correct.

## Supporting Software & System Readiness

This section lists the minimal, Windows-only software and system settings required to produce deterministic, production-quality label output that matches Preview, PDF and thermal printers.

Required software and drivers
- .NET 8 Desktop Runtime (required to run the WPF application). Development requires the .NET 8 SDK.
- Vendor printer driver for the target thermal printer (e.g., SNBC/TVS or the device used in clinic). The driver must:
  - Support a custom/media size of 50 mm × 30 mm or allow defining a matching paper/media size.
  - Allow direct printing without automatic scaling or margins added by the driver.
  - Support raw/ passthrough printing if TSPL is used (for RawPrinterHelper/Direct raw sends).
- PDF viewer capable of printing at 100% (Actual Size) with scaling disabled (see checklist below).

System Readiness Checklist (practical, do these before QA)
- Windows display scaling: set Windows Display Scaling to 100% (Settings → Display → Scale). Non‑100% scaling changes WPF DPI assumptions and can lead to incorrect on‑screen layout and exported raster sizes.
- Printer driver media/paper size and scaling:
  - Create or confirm a custom media size of 50 mm × 30 mm in the printer driver settings.
  - Ensure the driver is configured to use "Actual Size" or no scaling; disable "Fit to Page" or any auto‑scale options.
  - Verify the driver does not add unneeded margins; set printable origin to (0,0) or minimal hardware margin if supported.
- PDF viewer print scaling: when printing the exported PDF, always select "Actual Size" / "100%" in the viewer print dialog. Do not use "Fit" or "Shrink to Printable Area" — those options rescale the embedded PNG and break 1:1 physical sizing.
- Font availability:
  - The application uses `Segoe UI` as the default UI font. For the vector/text fallback path (PDF vector export or TSPL text attempts), ensure `Segoe UI` is present on the machine used for export/printing; missing fonts change metrics and can break centering and auto‑shrink behavior.
  - The preferred raster PDF path embeds a bitmap so font availability is less critical for visual parity; still validate vector fallback only when the raster path cannot be used.

If any item above does not hold on a target machine, document the discrepancy and revalidate printed test patches before approving changes to defaults or calibration values.

## License / Notes

Check the repository `LICENSE` file if present. If no license file is present, treat the code as "All Rights Reserved" and request permission before reuse.

---

If you join the project, read the files listed under `/Services` and `/Helpers` first; they contain the deterministic layout and conversion math that must remain stable for correct printing.
