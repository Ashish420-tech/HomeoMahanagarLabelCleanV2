# HomeoMahanagarLabelCleanV2 - Project Execution Flow

**Document Version:** 1.0  
**Date:** January 2024  
**Document Type:** Process Documentation  
**Audience:** Developers, QA Engineers, Technical Managers  

---

## Introduction

This document explains the complete execution flows within the HomeoMahanagarLabelCleanV2 system. Each flow is described step-by-step in plain language, suitable for both technical and non-technical readers.

**Key Flows Covered:**
1. Application Startup Flow
2. User Interaction Flow (Input → Preview → Print)
3. Rendering Flow (WPF → DPI → Bitmap)
4. PDF Export Flow
5. Print Execution Flow
6. Test Execution Flow
7. Release & Validation Flow

---

## Flow 1: Application Startup

### Purpose
Initialize the application, load configuration, and prepare the user interface for operation.

### Step-by-Step Flow

**Step 1: Application Launch**
- **Input:** User double-clicks application icon or runs from command line
- **Processing:** Windows loads executable (`HomeoMahanagarLabelCleanV2.exe`)
- **Output:** .NET 8 runtime initializes, WPF framework loads

**Step 2: Initialize Logging (DEBUG builds only)**
- **Input:** Application startup event
- **Processing:**
  1. Create log directory: `%LOCALAPPDATA%/HomeoMahanagarLabelCleanV2/Logs/`
  2. Initialize `AppLogger` (basic file logging)
  3. Write startup message: `"Application starting"`
- **Output:** Log file ready for error/event recording

**Step 3: Enable Session Event Logging (DEBUG builds only)**
- **Input:** Application startup event
- **Processing:**
  1. Create session log directory: `%LOCALAPPDATA%/HomeoMahanagarLabelCleanV2/SessionLogs/`
  2. Start `SessionEventLogger` (detailed performance/event logging)
  3. Subscribe to console output (for development debugging)
  4. Write initialization message: `"Session event logging initialized"`
- **Output:** Session logging active, events recorded to timestamped log file

**Step 4: Start UI Thread Watchdog (DEBUG builds only)**
- **Input:** Application `Dispatcher` (UI thread)
- **Processing:**
  1. Start background timer (checks UI thread every 50ms)
  2. Monitor for UI stalls > 100ms
  3. Log warnings if stalls detected
- **Output:** UI responsiveness monitoring active

**Step 5: Register Global Error Handlers**
- **Input:** Application domain and dispatcher events
- **Processing:**
  1. Attach handler: `DispatcherUnhandledException` (UI thread crashes)
  2. Attach handler: `UnhandledException` (non-UI thread crashes)
  3. Attach handler: `UnobservedTaskException` (async task failures)
- **Output:** All exceptions caught and logged

**Step 6: Seed Medicine Database (First Run)**
- **Input:** Check if medicine storage is empty
- **Processing:**
  1. Look for `remedies.xlsx` file next to application executable
  2. If found, import medicine names from Excel
  3. Save to local storage: `%LOCALAPPDATA%/HomeoMahanagarLabelCleanV2/storage.json`
- **Output:** Medicine database initialized (if needed)

**Step 7: Load Main Window**
- **Input:** Application initialized successfully
- **Processing:**
  1. Create `MainView` WPF window
  2. Bind `LabelViewModel` to UI controls
  3. Load saved state (last used medicine, printer selection)
  4. Display window
- **Output:** Main window visible, application ready for user input

**Total Startup Time:** < 5 seconds (typical: 1-2 seconds)

---

## Flow 2: User Interaction Flow (Input → Preview → Print)

### Purpose
Allow user to enter medicine information, preview label, and print or export.

### Step-by-Step Flow

**Step 1: User Enters Medicine Name**
- **Input:** User types in "Medicine Name" textbox
- **Processing:**
  1. Trigger auto-complete suggestions from medicine database
  2. Update `LabelViewModel.MedicineName` property
  3. Trigger preview refresh
- **Output:** Auto-complete dropdown displayed (if matches found)

**Step 2: User Enters Potency, Dosage, Timing**
- **Input:** User types in respective textboxes
- **Processing:**
  1. Update `LabelViewModel` properties
  2. Trigger preview refresh for each change
- **Output:** Preview updates in real-time

**Step 3: Select Shop Name**
- **Input:** User selects from dropdown or enters custom text
- **Processing:**
  1. Update `LabelViewModel.ShopName` property
  2. Trigger preview refresh
- **Output:** Shop name appears in preview

**Step 4: Preview Update (Real-Time)**
- **Input:** Any input field changed
- **Processing:**
  1. `LabelViewModel` calls `LabelTextComposer.Compose()`
     - Wraps long text (e.g., "ARNICA MONTANA" → "ARNICA\nMONTANA")
     - Normalizes whitespace
     - Enforces maximum lengths
  2. `LabelViewModel.CreateCanvasItems()` creates layout items:
     - Medicine name → position (x=86, y=6), font size 14, centered
     - Potency → position (x=86, y=26), font size 11, centered
     - Dosage → position (x=86, y=42), font size 10, centered
     - Timing → position (x=86, y=52), font size 10, centered
     - Shop → position (x=86, y=70), font size 10, centered
  3. `PrintLabelView.RenderItems()` draws WPF visual:
     - Clears canvas
     - Creates `TextBlock` for each item
     - Positions at scaled coordinates
     - Updates layout (`Measure → Arrange → UpdateLayout`)
  4. WPF renders visual to screen
- **Output:** Preview window shows updated label (< 100ms delay)

**Step 5: User Clicks "Print" Button**
- **Input:** Button click event
- **Processing:**
  1. Validate inputs (all required fields filled)
  2. If invalid, show error message and stop
  3. If valid, proceed to Print Flow (see Flow 5)
- **Output:** Print job started OR error message displayed

**Step 6: User Clicks "Export PDF" Button**
- **Input:** Button click event
- **Processing:**
  1. Validate inputs
  2. Show file save dialog (default: Desktop, filename: `Label_[MedicineName].pdf`)
  3. User selects location and filename
  4. Proceed to PDF Export Flow (see Flow 4)
- **Output:** PDF file created at selected location

---

## Flow 3: Rendering Flow (WPF → DPI → Bitmap)

### Purpose
Convert WPF visual representation into a rasterized bitmap at target DPI for PDF/print.

### Step-by-Step Flow

**Step 1: Determine Target Size**
- **Input:** Physical label size (50mm × 30mm)
- **Processing:**
  1. Convert mm to inches: 50mm ÷ 25.4 = 1.97 inches, 30mm ÷ 25.4 = 1.18 inches
  2. Convert inches to DIPs: 1.97" × 96 DIP/inch = 189 DIP, 1.18" × 96 DIP/inch = 113 DIP
- **Output:** Logical size = 189 DIP × 113 DIP

**Step 2: Create PrintLabelView**
- **Input:** Logical size (189 × 113 DIP)
- **Processing:**
  1. Instantiate `PrintLabelView` WPF control
  2. Set `Width = 189`, `Height = 113`
  3. Call `RenderItems(canvasItems)` to populate visual tree
- **Output:** WPF visual ready for layout

**Step 3: WPF Layout Pass**
- **Input:** PrintLabelView with content
- **Processing:**
  1. Call `Measure(new Size(189, 113))` → WPF calculates desired sizes
  2. Call `Arrange(new Rect(0, 0, 189, 113))` → WPF positions elements
  3. Call `UpdateLayout()` → Forces synchronous layout completion
- **Output:** Visual tree fully laid out, ready for rendering

**Step 4: Determine Pixel Dimensions at Target DPI**
- **Input:** Logical size (189 × 113 DIP), target DPI (e.g., 300 DPI for PDF)
- **Processing:**
  1. Calculate scale factor: 300 DPI ÷ 96 DPI = 3.125
  2. Calculate pixel dimensions: 189 DIP × 3.125 = 590.625 → 591 pixels
     113 DIP × 3.125 = 353.125 → 354 pixels
- **Output:** Pixel dimensions = 591 × 354

**Step 5: Create RenderTargetBitmap**
- **Input:** Pixel dimensions (591 × 354), target DPI (300)
- **Processing:**
  1. Instantiate `RenderTargetBitmap(591, 354, 300, 300, PixelFormats.Pbgra32)`
  2. This creates an in-memory bitmap canvas at specified DPI
- **Output:** Empty bitmap ready for rendering

**Step 6: Render WPF Visual to Bitmap**
- **Input:** RenderTargetBitmap, PrintLabelView
- **Processing:**
  1. Call `renderBitmap.Render(printLabelView)`
  2. WPF rendering engine rasterizes visual tree:
     - Text glyphs rendered with ClearType
     - Positions calculated at target DPI
     - Anti-aliasing applied
  3. Bitmap pixels populated with RGBA values
- **Output:** Bitmap contains rendered label at 300 DPI

**Step 7: Encode Bitmap to PNG**
- **Input:** RenderTargetBitmap
- **Processing:**
  1. Create `PngBitmapEncoder()`
  2. Add frame: `encoder.Frames.Add(BitmapFrame.Create(renderBitmap))`
  3. Save to memory stream: `encoder.Save(memoryStream)`
  4. Extract bytes: `byte[] pngBytes = memoryStream.ToArray()`
- **Output:** PNG file as byte array

**Step 8: Use PNG Bytes**
- **Input:** PNG byte array
- **Processing:**
  - **For PDF Export:** Embed PNG in PDF document (PdfSharp)
  - **For Print:** Send PNG to printer driver (PrintService)
- **Output:** PNG ready for final destination

**Performance:** Typical rendering time: 8-45ms (depends on DPI and content complexity)

---

## Flow 4: PDF Export Flow

### Purpose
Export label to PDF format for archival, email, or printing from PDF viewer.

### Step-by-Step Flow

**Step 1: User Triggers Export**
- **Input:** "Export PDF" button click
- **Processing:**
  1. Show Windows file save dialog
  2. Default location: Desktop
  3. Default filename: `Label_[MedicineName]_[Timestamp].pdf`
  4. User selects location and clicks "Save"
- **Output:** File path selected

**Step 2: Render Label to PNG**
- **Input:** Current label canvas items, target DPI = 300
- **Processing:**
  1. Follow Rendering Flow (Flow 3) steps 1-7
  2. Generate PNG bytes at 300 DPI (high quality for printing)
- **Output:** PNG byte array (591 × 354 pixels at 300 DPI)

**Step 3: Create PDF Document**
- **Input:** PdfSharp library loaded
- **Processing:**
  1. Create `PdfDocument` instance
  2. Set page size: 50mm × 30mm (converted to PDF points: 141.73 × 85.04 points)
  3. Add page: `PdfPage page = document.AddPage()`
  4. Set page dimensions to match label
- **Output:** Empty PDF document with correct page size

**Step 4: Embed PNG in PDF**
- **Input:** PNG bytes, PDF page
- **Processing:**
  1. Create `XImage` from PNG bytes: `XImage.FromStream(pngStream)`
  2. Get PDF graphics context: `XGraphics gfx = XGraphics.FromPdfPage(page)`
  3. Draw image to fill page: `gfx.DrawImage(image, 0, 0, pageWidth, pageHeight)`
  4. Image scaled to fit page exactly
- **Output:** PDF page contains embedded PNG

**Step 5: Save PDF File**
- **Input:** PDF document, file path
- **Processing:**
  1. Call `document.Save(filePath)`
  2. PdfSharp writes PDF file to disk
  3. File handle released
- **Output:** PDF file created at selected location

**Step 6: Open PDF (Optional)**
- **Input:** User preference (not always done)
- **Processing:**
  1. Start default PDF viewer: `Process.Start(filePath)`
  2. Windows opens file in Adobe Reader, Edge, or Chrome
- **Output:** PDF displayed for user verification

**Step 7: Notify User**
- **Input:** Export success or failure
- **Processing:**
  1. Show success message: "PDF saved to [path]"
  2. OR show error message if failure occurred
- **Output:** User notified of result

**Performance:** Typical export time: 50-200ms

---

## Flow 5: Print Execution Flow

### Purpose
Send label to thermal printer for physical output.

### Step-by-Step Flow

**Step 1: User Initiates Print**
- **Input:** "Print" button click
- **Processing:**
  1. Validate inputs (required fields filled)
  2. Get selected printer from dropdown
  3. Check printer status (online/offline)
- **Output:** Print job ready OR error displayed

**Step 2: Render Label to PNG**
- **Input:** Current label canvas items, target DPI = 300
- **Processing:**
  1. Follow Rendering Flow (Flow 3) steps 1-7
  2. Generate PNG bytes at 300 DPI
- **Output:** PNG byte array

**Step 3: Create Print Job**
- **Input:** PNG bytes, printer name, label size
- **Processing:**
  1. Create `PrintService` instance
  2. Set printer: `PrintService.SetPrinter(printerName)`
  3. Set media size: 50mm × 30mm
  4. Create print job: `printJob = new PrintJob(pngBytes, size)`
- **Output:** Print job object created

**Step 4: Queue Print Job**
- **Input:** Print job
- **Processing:**
  1. Add to print queue: `PrintService.EnqueueJob(printJob)`
  2. Print queue processes jobs sequentially (FIFO)
- **Output:** Job in queue, awaiting execution

**Step 5: Execute Print Job**
- **Input:** Print job at front of queue
- **Processing:**
  1. Send PNG to printer driver using Windows Print API
  2. Printer driver converts PNG to printer-specific format (TSPL or similar)
  3. Commands sent to printer via USB/network
  4. Thermal printer heats print head to create image on label
- **Output:** Physical label printed

**Step 6: Verify Print Completion**
- **Input:** Printer status feedback
- **Processing:**
  1. Wait for printer "job complete" signal (or timeout after 30 seconds)
  2. If success, remove job from queue
  3. If failure, retry or display error
- **Output:** Print confirmed OR error reported

**Step 7: Notify User**
- **Input:** Print result
- **Processing:**
  1. Show success message: "Label printed successfully"
  2. OR show error: "Print failed: [reason]" with retry option
- **Output:** User notified

**Performance:** Typical print time: 2-5 seconds (including printer warm-up)

---

## Flow 6: Test Execution Flow

### Purpose
Execute automated tests and generate comprehensive test report.

### Step-by-Step Flow

**Step 1: Trigger Test Suite**
- **Input:** Command: `dotnet test` or `dotnet run --project Tests/Runners/MultiDpiTestRunner.cs`
- **Processing:**
  1. .NET test runner locates test project: `HomeoMahanagarLabelCleanV2.Tests.csproj`
  2. Compile test project (if needed)
  3. Discover test methods (xUnit attributes: `[Fact]`, `[StaFact]`, `[Trait]`)
- **Output:** Test execution begins

**Step 2: Collect Environment Information**
- **Input:** System query
- **Processing:**
  1. Detect OS version: Read Windows registry (`ProductName`, `CurrentBuild`)
  2. Detect Windows DPI: Query system DPI (default 96 if query fails)
  3. Calculate scaling: 96 DPI → 100%, 120 DPI → 125%, 144 DPI → 150%
  4. Get machine name: `Environment.MachineName`
  5. Get .NET version: `Environment.Version`
- **Output:** Environment metadata collected

**Step 3: Execute Unit Tests (Level 1)**
- **Input:** Test filter: `Category=Unit`
- **Processing:**
  1. Run `PrintConstantsTests` (17 tests):
     - Validate mm → DIP conversions
     - Validate DIP → mm conversions
     - Validate known values (50mm = 189 DIP at 96 DPI)
  2. Run `LabelTextComposerTests` (if exists):
     - Validate text wrapping logic
     - Validate normalization
  3. Collect pass/fail results
  4. Record duration
- **Output:** Unit test results (e.g., 17/17 passed, 0.8s)

**Step 4: Execute Snapshot Tests (Level 3a)**
- **Input:** Test filter: `Category=Snapshot`
- **Processing:**
  1. Run snapshot tests (3 tests):
     - `PrintLabelView_FixedInput_ProducesDeterministicOutput`:
       * Render label with standard inputs
       * Compare PNG to baseline: `Tests/SnapshotTests/Baselines/StandardLabel.png`
       * Calculate pixel difference percentage
     - `PrintLabelView_EmptyInputs_ProducesDeterministicOutput`:
       * Render label with empty inputs
       * Compare to baseline: `EmptyLabel.png`
  2. Collect results (pass if < 0.1% pixel difference)
- **Output:** Snapshot test results (e.g., 3/3 passed)

**Step 5: Execute Multi-DPI Tests (Level 3b)**
- **Input:** Test filter: `Category=DPI`
- **Processing:**
  1. For each DPI (96, 120, 144):
     - Render `PrintLabelView` off-screen at target DPI
     - Validate pixel dimensions:
       * 96 DPI: Expected 189×113, Actual ___
       * 120 DPI: Expected 237×142, Actual ___
       * 144 DPI: Expected 284×170, Actual ___
     - Validate physical size invariance (50mm × 30mm)
     - Compare to DPI-specific baseline:
       * `StandardLabel_DPI96.png`
       * `StandardLabel_DPI120.png`
       * `StandardLabel_DPI144.png`
  2. Collect results (7 tests total)
- **Output:** Multi-DPI test results (e.g., 6/7 passed, 1 failed at 144 DPI)

**Step 6: Collect Performance Metrics**
- **Input:** Session log files (if DEBUG build run recently)
- **Processing:**
  1. Parse session logs from `%LOCALAPPDATA%/HomeoMahanagarLabelCleanV2/SessionLogs/`
  2. Extract performance events:
     - `[Performance] PrintLabelView.RenderItems: Xms`
     - `[Performance] PdfHelper.ExportLabelToPdf: Yms`
  3. Calculate averages:
     - Avg render time
     - Avg PDF export time
  4. Detect regressions (compare to thresholds)
- **Output:** Performance metrics summary

**Step 7: Check Manual Validation Status**
- **Input:** File check
- **Processing:**
  1. Look for `Manual_DPI_Test_Results.md` in project root
  2. If found, parse validation results:
     - "100% Scaling: PASS" → Manual validation[0].Status = "PASS"
     - "125% Scaling: PASS" → Manual validation[1].Status = "PASS"
     - "150% Scaling: NOT VERIFIED" → Manual validation[2].Status = "NOT VERIFIED"
  3. If not found, all statuses = "NOT VERIFIED"
- **Output:** Manual validation status array

**Step 8: Analyze Results & Determine Release Decision**
- **Input:** All test results, manual validation status
- **Processing:**
  1. Apply decision logic:
     - **IF** unit tests failed → **NO-GO** (critical)
     - **ELSE IF** manual validation failed → **NO-GO** (printed output wrong)
     - **ELSE IF** all tests passed AND manual validation complete → **GO**
     - **ELSE** → **CONDITIONAL GO** (manual validation pending)
  2. Generate suggestions based on failures:
     - Unit test failures → "Fix logic immediately, blocking"
     - Snapshot failures → "Update baseline after printer validation"
     - DPI failures → "Test at specific scaling level manually"
     - Manual validation missing → "Complete manual testing checklist"
- **Output:** Release decision + suggestions list

**Step 9: Generate Test Report**
- **Input:** Aggregated results, decision, suggestions
- **Processing:**
  1. Format report with sections:
     - [ENVIRONMENT]: OS, DPI, machine name
     - [AUTOMATED TEST RESULTS]: Pass/fail by category
     - [DPI ANALYSIS]: Pixel dimensions, physical size, baselines
     - [PERFORMANCE]: Render times, export times
     - [MANUAL VALIDATION STATUS]: 100%, 125%, 150% scaling results
     - [SUGGESTIONS]: Numbered list of remediation steps
     - [FINAL DECISION]: GO/CONDITIONAL GO/NO-GO + justification
  2. Create filename: `HomeoLabel_TestReport_yyyy-MM-dd_HH-mm.log`
  3. Save to Desktop: `C:\Users\[Username]\Desktop\`
- **Output:** Test report file created

**Step 10: Print Summary to Console**
- **Input:** Test report data
- **Processing:**
  1. Print:
     ```
     Test Execution Complete!
       Total Tests: 27
       Passed: 25 ✅
       Failed: 2 ❌
       Duration: 8.7s
     
     Report saved to Desktop:
       C:\Users\Ashish\Desktop\HomeoLabel_TestReport_2024-01-15_16-45.log
     
     Release Recommendation: ⚠️ CONDITIONAL GO - Manual validation required
     ```
  2. Exit with code: 0 (GO/CONDITIONAL) or 1 (NO-GO)
- **Output:** Console summary + exit code

**Total Test Execution Time:** 8-15 seconds (typical)

---

## Flow 7: Release & Validation Flow

### Purpose
Ensure software quality before production release through automated and manual validation.

### Step-by-Step Flow

**Step 1: Developer Completes Feature/Fix**
- **Input:** Code changes committed to Git
- **Processing:**
  1. Developer creates pull request (PR)
  2. Code review requested
- **Output:** PR awaiting review

**Step 2: Automated Build & Test (CI/CD)**
- **Input:** PR created/updated
- **Processing:**
  1. CI/CD system (e.g., GitHub Actions) triggers
  2. Checkout code from repository
  3. Run `dotnet build` → Check for compilation errors
  4. Run `dotnet test` → Execute all automated tests
  5. Generate test report
- **Output:** Build status (success/fail) + test results

**Step 3: Code Review**
- **Input:** PR + test results
- **Processing:**
  1. Reviewer checks:
     - Code quality (style, documentation)
     - Logic correctness
     - Test coverage
     - No production feature changes (unless intentional)
  2. Reviewer approves or requests changes
- **Output:** PR approved OR changes requested

**Step 4: Merge to Main Branch**
- **Input:** Approved PR
- **Processing:**
  1. Developer merges PR
  2. Code integrated into main branch
  3. Git tag created (e.g., `v1.2.0`)
- **Output:** Main branch updated

**Step 5: Run Full Test Suite**
- **Input:** Main branch code
- **Processing:**
  1. QA engineer runs: `dotnet run --project Tests/Runners/MultiDpiTestRunner.cs`
  2. Follow Test Execution Flow (Flow 6)
  3. Review test report on Desktop
- **Output:** Test report with release decision

**Step 6: Review Test Report**
- **Input:** Test report file
- **Processing:**
  1. QA lead opens report
  2. Check [FINAL DECISION] section:
     - **GO:** Proceed to deployment
     - **CONDITIONAL GO:** Proceed to manual validation
     - **NO-GO:** Block release, fix issues
  3. Review [SUGGESTIONS] for action items
- **Output:** Decision to proceed or block

**Step 7: Perform Manual Validation (if CONDITIONAL GO)**
- **Input:** CONDITIONAL GO decision
- **Processing:**
  1. QA engineer sets Windows display scaling to 100%
     - Right-click Desktop → Display settings → Scale: 100%
     - Log out and log back in
  2. Launch application
  3. Enter standard test label data:
     - Medicine: ARNICA MONTANA
     - Potency: 200 CH
     - Dosage: 5 GLOB
     - Timing: MORNING/NOON/NIGHT
     - Shop: HOMEO MAHANAGAR
  4. Preview label on screen
  5. Export to PDF, print PDF from viewer
  6. Print directly from application
  7. **Validate:** All three outputs (preview, PDF print, direct print) match
  8. Document result in `Manual_DPI_Test_Results.md`:
     ```markdown
     ### Test 1: 100% Scaling (96 DPI)
     - Preview rendering: PASS
     - PDF export: PASS
     - Print output: PASS
     - Notes: All text sharp and centered
     ```
  9. Repeat for 125% and 150% scaling
- **Output:** Manual validation documented (PASS/FAIL)

**Step 8: Re-Run Test Suite (After Manual Validation)**
- **Input:** Updated `Manual_DPI_Test_Results.md`
- **Processing:**
  1. Run test suite again
  2. Test report now includes manual validation status
  3. Decision should change to **GO** if all manual tests passed
- **Output:** Updated test report with GO decision

**Step 9: Create Release Package**
- **Input:** GO decision
- **Processing:**
  1. Build in Release configuration: `dotnet build -c Release`
  2. Publish application: `dotnet publish -c Release -r win-x64 --self-contained false`
  3. Create installer (optional): MSI or ZIP package
  4. Archive test report with release
- **Output:** Deployable release package

**Step 10: Deploy to Production**
- **Input:** Release package
- **Processing:**
  1. System admin copies files to production machine
  2. Install .NET 8 Desktop Runtime (if not present)
  3. Run application to verify
  4. Notify users of update
- **Output:** Application deployed

**Step 11: Post-Release Monitoring**
- **Input:** Application in use
- **Processing:**
  1. Monitor for user-reported issues
  2. Review session logs (if DEBUG build)
  3. Track performance metrics
- **Output:** Ongoing quality monitoring

**Total Release Cycle:** 1-3 days (depends on manual validation availability)

---

## Summary of Critical Flows

| Flow | Trigger | Duration | Key Output |
|------|---------|----------|------------|
| **Startup** | User launches app | < 5s | Main window displayed |
| **User Interaction** | User enters data | Real-time | Preview updates |
| **Rendering** | Preview/Export/Print | 8-45ms | PNG bitmap at target DPI |
| **PDF Export** | Export button | 50-200ms | PDF file on Desktop |
| **Print** | Print button | 2-5s | Physical label |
| **Test Execution** | dotnet test | 8-15s | Test report on Desktop |
| **Release Validation** | Pre-release | 1-3 days | GO/NO-GO decision |

---

## Glossary of Terms

| Term | Explanation |
|------|-------------|
| **DIP** | Device Independent Pixel - WPF's logical unit (1 DIP = 1/96 inch) |
| **DPI** | Dots Per Inch - Display or printer resolution |
| **PNG** | Portable Network Graphics - Lossless image format |
| **PDF** | Portable Document Format - Universal document format |
| **WPF** | Windows Presentation Foundation - Microsoft's UI framework |
| **XAML** | eXtensible Application Markup Language - WPF UI definition language |
| **Snapshot Test** | Automated test comparing output to baseline image |
| **Baseline** | Reference image for comparison |
| **STA Thread** | Single-Threaded Apartment - Required for WPF UI tests |

---

## Document Control

**Document Owner:** Technical Documentation Team  
**Review Cycle:** Per major release  
**Last Updated:** January 2024  
**Version:** 1.0

---

**END OF DOCUMENT**
