# Multi-DPI Testing Guide

## Overview

This guide describes how to test label rendering correctness across different Windows display scaling settings (DPI levels).

**Two-Tier Approach:**
1. **Automated Tests** - Off-screen rendering at multiple DPI values (fast, deterministic)
2. **Manual Validation** - Visual inspection on actual scaled desktops (final verification)

---

## Automated Multi-DPI Tests

### What They Test

✅ **Automated tests validate:**
- WPF layout math at different DPI scales (96, 120, 144 DPI)
- DIPs → Pixels conversion correctness
- Text positioning and sizing under scaling
- Control alignment consistency
- Bitmap rasterization output

❌ **Automated tests DO NOT validate:**
- Actual Windows Desktop Window Manager (DWM) rendering
- GPU-specific rendering behavior
- ClearType font hinting variations
- Real printer DPI handling
- Hardware-accelerated composition effects

### Running Automated Tests

```bash
# Run all DPI tests
dotnet test --filter Category=DPI

# Run specific test
dotnet test --filter "FullyQualifiedName~MultiDpiRenderingTests.PrintLabelView_RenderAt96Dpi"
```

### Expected Results

**Pixel Dimensions at Standard DPI Levels:**

| DPI | Scaling | Label Width (px) | Label Height (px) | Physical Size |
|-----|---------|------------------|-------------------|---------------|
| 96  | 100%    | 189              | 113               | 1.97" × 1.18" |
| 120 | 125%    | 237              | 142               | 1.97" × 1.18" |
| 144 | 150%    | 284              | 170               | 1.97" × 1.18" |
| 192 | 200%    | 378              | 226               | 1.97" × 1.18" |

**Key Invariant:** Physical size (inches) must be identical across all DPI settings.

---

## Manual Validation Matrix

### Why Manual Testing is Required

Automated tests cannot validate:
- Real Windows DWM rendering pipeline
- Font anti-aliasing (ClearType) at different scales
- Hardware-specific rendering quirks
- User perception of sharpness/clarity
- Actual print output at scaled resolutions

### Minimal Validation Matrix

Test on **one machine** with **three DPI settings**:

| Test Case | Windows Scaling | DPI | Validate |
|-----------|-----------------|-----|----------|
| **Test 1** | 100% (Recommended) | 96 | Baseline rendering |
| **Test 2** | 125% | 120 | Common laptop scaling |
| **Test 3** | 150% | 144 | High-DPI laptop/4K display |

**Optional (if 4K displays are used):**
- Test 4: 200% (192 DPI) - 4K/UHD displays

### Setup: Changing Windows Display Scaling

**Windows 11:**
1. Right-click Desktop → Display settings
2. Scroll to "Scale & layout"
3. Select scaling percentage (100%, 125%, 150%, etc.)
4. **Important:** Log out and log back in (some apps require this)

**Windows 10:**
1. Settings → System → Display
2. Change "Scale and layout"
3. Sign out and sign back in

### Manual Test Procedure

**For each DPI level (100%, 125%, 150%):**

1. **Set Windows scaling** (see above)
2. **Launch application**
3. **Load standard test label:**
   - Medicine: ARNICA MONTANA
   - Potency: 200 CH
   - Dose: 5 GLOB
   - Time: MORNING/NOON/NIGHT
   - Shop: HOMEO MAHANAGAR

4. **Visual Inspection Checklist:**

   ✅ **Layout:**
   - [ ] All 5 lines visible
   - [ ] Text centered horizontally
   - [ ] Vertical spacing consistent
   - [ ] No text clipping or overlap

   ✅ **Text Rendering:**
   - [ ] Text is sharp and readable
   - [ ] Font sizes appear proportional
   - [ ] No pixelation or blurriness
   - [ ] ClearType rendering looks correct

   ✅ **Preview Window:**
   - [ ] Label preview fills expected area
   - [ ] No scaling artifacts (stretched/squashed)
   - [ ] Window controls (buttons, textboxes) are usable

5. **Export PDF:**
   - [ ] PDF export completes successfully
   - [ ] Open PDF in viewer (Adobe Reader, Edge)
   - [ ] PDF content matches preview
   - [ ] Text is sharp at 100% zoom
   - [ ] Print PDF to physical printer
   - [ ] Printed output matches preview

6. **Direct Print:**
   - [ ] Print directly from application
   - [ ] Printed label matches preview
   - [ ] Text alignment correct
   - [ ] No scaling distortions

7. **Screenshot Comparison (optional):**
   - Take screenshot of preview at each DPI
   - Save to `Manual_Validation_Screenshots/`
   - Compare side-by-side (text should be same physical size on screen)

### Acceptance Criteria

**Pass if:**
- All 5 text lines visible and readable at all DPI levels
- Text centering and alignment consistent
- PDF export matches preview
- Printed output matches preview
- No visual glitches or layout breakage

**Fail if:**
- Text clipped, overlapping, or missing
- Layout shifts significantly between DPI levels
- PDF export differs from preview
- Printed output has scaling distortions

### Recording Results

Document findings in `Manual_DPI_Test_Results.md`:

```markdown
## Manual DPI Validation Results

**Date:** 2024-01-15
**Tester:** [Your Name]
**OS:** Windows 11 23H2
**Machine:** Dell XPS 15 (1920×1080)

### Test 1: 100% Scaling (96 DPI)
- ✅ Preview rendering: PASS
- ✅ PDF export: PASS
- ✅ Print output: PASS
- Notes: Baseline - all text sharp and centered

### Test 2: 125% Scaling (120 DPI)
- ✅ Preview rendering: PASS
- ✅ PDF export: PASS
- ⚠️  Print output: MINOR ISSUE - slight vertical shift (~1mm)
- Notes: Text rendering slightly softer but acceptable

### Test 3: 150% Scaling (144 DPI)
- ✅ Preview rendering: PASS
- ✅ PDF export: PASS
- ✅ Print output: PASS
- Notes: Text very sharp, no issues detected
```

---

## Troubleshooting DPI Issues

### Issue: Text Clipped at 125%/150%

**Symptom:** Text is cut off or overlapping at higher DPI

**Possible Causes:**
- Fixed pixel widths instead of DIP-based layout
- Hard-coded font sizes not scaling
- Canvas positioning logic assumes 96 DPI

**Fix:**
- Ensure all layout uses DIPs, not pixels
- Use `PrintConstants.MmToDip()` for sizing
- Test with `MultiDpiRenderingTests` to catch early

### Issue: Preview Blank at High DPI

**Symptom:** White/empty preview at 150%/200% scaling

**Possible Causes:**
- `RenderTargetBitmap` exceeds maximum dimensions
- Out-of-memory during bitmap creation
- WPF rendering pipeline failure

**Fix:**
- Check maximum bitmap size (2^16 pixels per dimension)
- Reduce label logical size if needed
- Add error handling around `RenderTargetBitmap`

### Issue: PDF Export Fails at High DPI

**Symptom:** PDF generation throws exception or produces corrupt file

**Possible Causes:**
- PNG byte array too large for PDF embedding
- PdfSharp library limits exceeded

**Fix:**
- Use consistent DPI for export (300 DPI recommended)
- Don't export at system DPI (always use fixed DPI)
- Current code already does this correctly

### Issue: Automated Tests Pass, Manual Tests Fail

**Symptom:** Tests green, but visual inspection shows issues

**Possible Causes:**
- Tests validate pixel dimensions, not visual correctness
- Font rendering differs from automated environment
- Hardware-specific rendering behavior

**Action:**
- Document manual findings
- Create new snapshot baseline if layout intentionally changed
- Report as potential limitation of automated tests

---

## Best Practices

### DO:
✅ Run automated tests on every commit
✅ Perform manual validation before major releases
✅ Test on at least 100%, 125%, 150% scaling
✅ Document manual test results
✅ Use consistent test data across DPI levels
✅ Compare screenshots side-by-side when debugging

### DON'T:
❌ Skip manual validation (automated tests have limits)
❌ Test only on 100% scaling
❌ Change system DPI during automated tests (not needed)
❌ Expect pixel-perfect rendering across machines
❌ Use UI automation for DPI testing (too flaky)

---

## Summary

**Automated Tests:**
- Fast, deterministic
- Validate layout math and pixel dimensions
- Catch regressions early
- Safe for CI/CD

**Manual Validation:**
- Final verification
- Validates real Windows rendering
- Catches visual issues automated tests miss
- Required before release

**Together:** Provide confidence that label rendering is correct across all supported Windows display scaling settings.
