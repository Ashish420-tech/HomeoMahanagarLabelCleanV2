# Testing & Quality Assurance Guide

## Overview

This document describes the testing strategy for the HomeoMahanagarLabelCleanV2 label printing system.

**Testing Philosophy:**
- Deterministic rendering is mandatory
- Preview, PDF, and Printer output MUST be identical
- Physical hardware validation required for regression approval
- Performance and responsiveness are quality attributes

## Testing Strategy

### 1. Unit Tests (Pure Logic)

**Purpose:** Validate deterministic calculations without UI or printer dependencies.

**Coverage:**
- `PrintConstants` conversion formulas
- `LabelTextComposer` wrapping and measurement logic
- Input normalization and validation

**Location:** `Tests/UnitTests/`

**Run:**
```bash
dotnet test --filter Category=Unit
```

**Key Tests:**
- `PrintConstantsTests` - DEFENSIVE: Fail if physical constants or conversion formulas change
- `LabelTextComposerTests` - Validate text composition and measurement

### 2. Snapshot Tests (Rendering Determinism)

**Purpose:** Detect unintended changes to rendered output via pixel comparison.

**Approach:**
- Render `PrintLabelView` off-screen at fixed DPI (96.0)
- Generate PNG baseline on first run
- Compare subsequent runs against baseline with pixel tolerance

**Location:** `Tests/SnapshotTests/`

**Baselines:** `Tests/SnapshotTests/Baselines/*.png`

**Run:**
```bash
dotnet test --filter Category=Snapshot
```

**Baseline Update Process:**
1. Delete existing baseline: `rm Tests/SnapshotTests/Baselines/StandardLabel.png`
2. Run test (will create new baseline)
3. **CRITICAL:** Print the new baseline on physical printer and verify correctness
4. Commit new baseline only after hardware validation

**Key Tests:**
- `PrintLabelView_FixedInput_ProducesDeterministicOutput` - Standard label rendering
- `PrintLabelView_EmptyInputs_ProducesDeterministicOutput` - Edge case rendering

### 3. Defensive Regression Tests

**Purpose:** Fail immediately if critical invariants are violated.

**Coverage:**
- Label physical dimensions (50mm × 30mm)
- Padding constants (2mm)
- Conversion formula stability
- DPI assumptions

**Location:** Embedded in `Tests/UnitTests/PrintConstantsTests.cs`

**Behavior:** These tests MUST fail loudly if constants change.

### 4. Performance Instrumentation (DEBUG-only)

**Purpose:** Monitor rendering and export performance; detect regressions.

**Instrumentation Points:**
- `PrintLabelView.RenderItems` - Label rendering duration
- `PdfHelper.RenderElementToPngBytes` - Rasterization duration
- `PdfHelper.ExportLabelToPdf` - Total PDF generation duration

**Logging:** Performance metrics logged via `AppLogger` with `[PERF]` prefix.

**Example Output:**
```
[PERF] PrintLabelView.RenderItems: 12ms (5 items)
[PERF] PdfHelper.RenderElementToPngBytes: 45ms (DPI=300, size=15234 bytes)
[PERF] PdfHelper.ExportLabelToPdf (raster): 78ms
```

**Removal:** All `#if DEBUG` blocks can be removed without affecting Release builds.

### 5. UI Thread Responsiveness Monitoring (DEBUG-only)

**Purpose:** Detect UI thread stalls that degrade user experience.

**Implementation:** `Diagnostics/UiThreadWatchdog.cs`

**Behavior:**
- Monitors WPF Dispatcher every 50ms
- Logs warnings if UI thread stalls > 100ms
- Zero overhead in Release builds
- Does NOT throw exceptions (diagnostic only)

**Usage:**
```csharp
// In App.xaml.cs OnStartup:
UiThreadWatchdog.Start();
```

**Warning Example:**
```
⚠️ UI thread stall detected: 153ms (threshold: 100ms)
```

### 6. Live Session Event Logging

**Purpose:** Real-time performance and diagnostic event streaming.

**Implementation:** `Logging/SessionEventLogger.cs`

**Features:**
- Thread-safe, low-allocation
- Live subscription support
- Optional non-blocking file logging
- Stopwatch-based duration tracking

**Usage:**

**Basic Logging:**
```csharp
SessionEventLogger.LogInfo("PrintLabel", "Starting print operation");
SessionEventLogger.LogWarning("PrintLabel", "Printer not found");
SessionEventLogger.LogError("PrintLabel", "Print failed", exception);
```

**Duration Tracking:**
```csharp
SessionEventLogger.LogStart("RenderLabel");
// ... work ...
SessionEventLogger.LogEnd("RenderLabel");
```

**Live Subscription:**
```csharp
SessionEventLogger.Subscribe(evt => 
{
    Console.WriteLine($"{evt.Timestamp:HH:mm:ss.fff} [{evt.Level}] {evt.Operation}: {evt.Message}");
    if (evt.DurationMs > 0)
        Console.WriteLine($"  Duration: {evt.DurationMs}ms");
});
```

**File Logging:**
```csharp
// In App.xaml.cs OnStartup:
string logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HomeoMahanagarLabelCleanV2", "SessionLogs");
SessionEventLogger.EnableFileLogging(logDir);
```

**Log Format:**
```
HH:mm:ss.fff [Level] Operation: Message (DurationMs)
```

## Test Execution

### Run All Tests
```bash
dotnet test
```

### Run Specific Categories
```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Snapshot
```

### Generate Coverage Report (optional)
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Quality Gates

### Before Committing Code Changes

1. **Unit Tests:** Must pass
   ```bash
   dotnet test --filter Category=Unit
   ```

2. **Snapshot Tests:** Must pass OR baseline updated with hardware validation
   ```bash
   dotnet test --filter Category=Snapshot
   ```

3. **Performance Check:** Review DEBUG logs for regressions
   - Rendering should complete < 50ms
   - PDF export should complete < 200ms

4. **UI Responsiveness:** No stalls > 100ms during manual testing

### Before Releasing to Production

1. All tests pass
2. Snapshot baselines validated on physical printer
3. Performance metrics reviewed (no >20% regression)
4. Manual print test on target thermal printer
5. Manual PDF export and print verification

## Common Test Scenarios

### Scenario: Font or Layout Change

**Impact:** Snapshot tests WILL fail

**Procedure:**
1. Make code change
2. Run snapshot tests (will fail)
3. Delete baseline: `rm Tests/SnapshotTests/Baselines/*.png`
4. Re-run tests (creates new baseline)
5. **CRITICAL:** Print new baseline on physical printer
6. Verify printed output matches preview and PDF
7. If correct, commit new baseline
8. If incorrect, revert code change

### Scenario: PrintConstants Change

**Impact:** Defensive regression tests WILL fail

**Procedure:**
1. Understand WHY constant must change (hardware requirement?)
2. Update constant in `PrintConstants.cs`
3. Run tests (will fail)
4. Update test assertions to match new values
5. **CRITICAL:** Validate on physical printer
6. Update all documentation referencing old values
7. Commit changes with detailed explanation

### Scenario: Performance Regression

**Impact:** DEBUG logs show increased duration

**Procedure:**
1. Compare `[PERF]` logs before/after change
2. If regression > 20%, investigate:
   - Is new allocation happening in hot path?
   - Is Measure/Arrange called multiple times?
   - Is file I/O blocking UI thread?
3. Profile using Visual Studio Performance Profiler
4. Fix or accept regression with justification

## Test Maintenance

### Adding New Tests

1. **Unit Test:**
   - Add to `Tests/UnitTests/`
   - Follow naming: `[ClassName]Tests.cs`
   - Use `[Fact]` and descriptive test names

2. **Snapshot Test:**
   - Add to `Tests/SnapshotTests/`
   - Render off-screen at TEST_DPI (96.0)
   - Create baseline with hardware validation
   - Document expected behavior in test comments

### Removing Instrumentation

All performance instrumentation is removable:

**DEBUG-only code blocks:**
```csharp
#if DEBUG
    var sw = Stopwatch.StartNew();
    // ... instrumentation ...
    sw.Stop();
    AppLogger.Log($"[PERF] {sw.ElapsedMilliseconds}ms");
#endif
```

**To remove:** Delete all `#if DEBUG` ... `#endif` blocks containing `[PERF]` logging.

**UiThreadWatchdog:** Simply don't call `UiThreadWatchdog.Start()` in App.xaml.cs.

**SessionEventLogger:** Remove `SessionEventLogger.*` calls as needed.

## Troubleshooting

### Snapshot Test Fails on Clean Checkout

**Cause:** Font rendering differences between machines (ClearType, DPI)

**Solution:**
- Run tests on same machine as baseline creation
- Or: Increase `PIXEL_TOLERANCE` in test
- Or: Re-create baseline and validate on printer

### UI Thread Watchdog False Positives

**Cause:** Debugger attached or system under load

**Solution:**
- Ignore warnings during debugging
- Increase `STALL_THRESHOLD_MS` if needed
- Focus on production-like environment results

### Performance Logs Not Appearing

**Cause:** Running Release build or instrumentation removed

**Solution:**
- Verify DEBUG configuration
- Check `AppLogger` output location (`%LOCALAPPDATA%/HomeoMahanagarLabelCleanV2/Logs`)

## Non-Testable Areas

**DO NOT attempt to unit test:**
- WPF Measure/Arrange behavior (system-dependent)
- Printer driver behavior (hardware-dependent)
- PDF rendering by external viewers (implementation-dependent)
- Actual thermal printer output (requires hardware)

**Instead:**
- Use snapshot tests for WPF rendering
- Use manual validation for printer output
- Document expected behavior in comments

## Summary

This testing strategy balances **automation** (unit/snapshot tests) with **reality** (hardware validation). The goal is to catch regressions early while acknowledging that deterministic printing requires physical verification.

**Key Principle:** Tests protect correctness; hardware validates reality.
