# QA Quick Reference Card

## 🚀 Quick Commands

### Run All Tests
```bash
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test --filter Category=Unit
```

### Run Snapshot Tests Only
```bash
dotnet test --filter Category=Snapshot
```

### Build Project
```bash
dotnet build
```

### Run Application (DEBUG)
```bash
dotnet run
```

---

## 📊 Performance Monitoring (DEBUG builds only)

### View Performance Logs
Check `AppLogger` output or Debug console for:
```
[PERF] PrintLabelView.RenderItems: 12ms (5 items)
[PERF] PdfHelper.RenderElementToPngBytes: 45ms (DPI=300, size=15234 bytes)
[PERF] PdfHelper.ExportLabelToPdf (raster): 78ms
```

### Session Event Logs
Location: `%LOCALAPPDATA%\HomeoMahanagarLabelCleanV2\SessionLogs\session_{timestamp}.log`

### UI Thread Warnings
Watch Debug output for:
```
⚠️ UI thread stall detected: 153ms (threshold: 100ms)
```

---

## 🔬 Common Development Tasks

### I changed layout code - what tests will fail?
- ✅ Run: `dotnet test --filter Category=Snapshot`
- 📋 If fails: Delete baseline, re-run to create new, validate on printer

### I changed PrintConstants - what breaks?
- ✅ Run: `dotnet test --filter Category=Unit`
- 📋 Tests WILL fail - update assertions AND validate on hardware

### I see performance regression in logs
- 📊 Compare `[PERF]` logs before/after
- 📋 If >20% regression, investigate using Profiler
- 📋 Check for new allocations or redundant Measure/Arrange calls

### I see UI thread stalls
- 📊 Note operation name from warning
- 📋 Review code for synchronous file I/O or heavy calculations
- 📋 Consider moving work off UI thread (if appropriate)

---

## 📝 Commit Checklist

Before committing code:
- [ ] `dotnet build` succeeds
- [ ] `dotnet test --filter Category=Unit` passes
- [ ] `dotnet test --filter Category=Snapshot` passes OR baseline updated
- [ ] Review DEBUG logs - no unexpected performance regressions
- [ ] No UI thread stalls during manual testing

---

## 🏷️ Baseline Update Process

### When snapshot test fails after layout change:

1. **Delete old baseline:**
   ```bash
   rm Tests/SnapshotTests/Baselines/StandardLabel.png
   ```

2. **Re-run test (creates new baseline):**
   ```bash
   dotnet test --filter Category=Snapshot
   ```

3. **CRITICAL: Validate on physical printer**
   - Print the new PNG baseline
   - Verify it matches preview and PDF
   - Check alignment, spacing, text content

4. **If correct:**
   ```bash
   git add Tests/SnapshotTests/Baselines/
   git commit -m "Update rendering baseline after [describe change]"
   ```

5. **If incorrect:**
   - Revert code change
   - Investigate why rendering differs from expectation

---

## 🎯 Performance Targets

**Rendering:**
- PrintLabelView.RenderItems: < 50ms

**PDF Export:**
- RenderElementToPngBytes: < 150ms
- ExportLabelToPdf: < 200ms

**UI Responsiveness:**
- No stalls > 100ms

**Regression Threshold:** +20% = investigate

---

## 🔧 Troubleshooting

### Snapshot test fails on my machine but passes in CI
- **Cause:** Font rendering differences (ClearType, DPI settings)
- **Solution:** Run on same machine as baseline creation OR increase tolerance

### UI Thread Watchdog gives false positives
- **Cause:** Debugger attached or system under heavy load
- **Solution:** Ignore during debugging; focus on production-like environment

### Performance logs not appearing
- **Cause:** Running Release build
- **Solution:** Switch to Debug configuration

### Tests can't find main project types
- **Cause:** Test project not referencing main project
- **Solution:** Check `Tests/*.csproj` has `<ProjectReference Include="..\HomeoMahanagarLabelCleanV2.csproj" />`

---

## 📚 Documentation

- `README.md` - Project overview and build instructions
- `TESTING.md` - Comprehensive testing guide
- `QA_IMPLEMENTATION.md` - Implementation details
- `ARCHITECTURE.md` - System architecture deep dive

---

## 🚫 What NOT to Do

- ❌ Don't modify PrintConstants without hardware validation
- ❌ Don't commit snapshot baseline without printing validation
- ❌ Don't ignore UI thread stalls in production-like scenarios
- ❌ Don't disable tests "because they're annoying"
- ❌ Don't mock WPF Measure/Arrange or printer behavior
- ❌ Don't add heavy external dependencies for testing

---

## ✅ Best Practices

- ✅ Run tests before every commit
- ✅ Review performance logs during development
- ✅ Validate snapshot baselines on physical printer
- ✅ Keep instrumentation removable (DEBUG-only)
- ✅ Document WHY constants changed (with validation proof)
- ✅ Use SessionEventLogger for debugging complex flows

---

## 🆘 Need Help?

1. Check `TESTING.md` for detailed procedures
2. Review `QA_IMPLEMENTATION.md` for implementation details
3. Check `ARCHITECTURE.md` for system design explanation
4. Ask: "Did I validate on physical printer?"

---

**Remember:** Tests protect correctness; hardware validates reality.
