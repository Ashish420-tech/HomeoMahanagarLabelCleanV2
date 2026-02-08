# HomeoMahanagarLabelCleanV2 - Project Requirements Specification

**Document Version:** 1.0  
**Date:** January 2024  
**Document Type:** Requirements Specification  
**Classification:** Project Documentation  

---

## 1. Introduction

### Purpose
This document specifies the functional and non-functional requirements for the HomeoMahanagarLabelCleanV2 label printing system. It serves as the authoritative source for development, testing, and validation activities.

### Scope
This specification covers all requirements for the Windows desktop application, including UI, rendering, PDF export, printing, testing, and quality assurance.

### Intended Audience
- Development Team
- QA Engineers
- Project Managers
- Business Stakeholders
- System Administrators

---

## 2. Functional Requirements

### FR-001: Medicine Label Input

**Requirement:** The system shall allow users to input medicine label information.

**Input Fields:**
| Field | Type | Validation | Mandatory |
|-------|------|------------|-----------|
| Medicine Name | Text | Max 100 characters | Yes |
| Potency | Text | Free text (e.g., "200 CH") | Yes |
| Dosage | Text | Free text (e.g., "5 GLOB") | Yes |
| Timing | Text | Free text (e.g., "MORNING/NOON") | Optional |
| Shop Name | Text | Pre-configured or custom | Yes |

**Acceptance Criteria:**
- ✅ All mandatory fields must be entered before preview/print
- ✅ Text exceeding max length must be truncated or wrapped
- ✅ Special characters must be preserved (e.g., "/", "-")

---

### FR-002: Label Preview

**Requirement:** The system shall display a real-time preview of the label before printing.

**Details:**
- Preview must reflect current input
- Preview updates immediately on text change
- Preview size must match physical label (50mm × 30mm)
- Preview must be visually identical to printed output

**Acceptance Criteria:**
- ✅ Preview visible in main window
- ✅ Preview updates within 100ms of input change
- ✅ Preview matches print output (< 0.1% pixel difference)

---

### FR-003: PDF Export

**Requirement:** The system shall export labels to PDF format for archival.

**Details:**
- PDF file saved to user-selected location (default: Desktop)
- PDF contains label at 300 DPI resolution
- PDF size matches physical label (50mm × 30mm)
- PDF viewable in standard PDF readers (Adobe, Edge, Chrome)

**Acceptance Criteria:**
- ✅ PDF export completes within 200ms
- ✅ PDF file size < 500 KB
- ✅ PDF content matches preview (< 0.1% difference)
- ✅ PDF prints correctly from PDF viewer

---

### FR-004: Direct Printing

**Requirement:** The system shall print labels directly to thermal label printers.

**Details:**
- Support for 203 DPI thermal label printers
- Custom media size: 50mm × 30mm
- Direct print mode (no Windows print dialog)
- Print queue management (handle multiple labels)

**Acceptance Criteria:**
- ✅ Print completes within 5 seconds
- ✅ Printed output matches preview (visual inspection)
- ✅ No scaling distortion
- ✅ Text alignment correct (centered)

---

### FR-005: Multi-DPI Rendering

**Requirement:** The system shall render labels correctly at multiple Windows display scaling levels.

**Supported DPI Levels:**
- 96 DPI (100% scaling)
- 120 DPI (125% scaling)
- 144 DPI (150% scaling)

**Details:**
- Physical label size must remain constant (50mm × 30mm)
- Pixel dimensions must scale proportionally
- Text must remain readable at all DPI levels

**Acceptance Criteria:**
- ✅ Label renders at all DPI levels
- ✅ Physical size invariant (±0.1mm)
- ✅ No text clipping or overlap
- ✅ Snapshot tests pass at all DPI levels

---

### FR-006: Medicine Database Management

**Requirement:** The system shall maintain a local database of medicine names.

**Details:**
- Import medicine list from Excel (.xlsx)
- Auto-complete suggestions during input
- Add/edit/delete medicines via admin panel
- Persistent storage (JSON file)

**Acceptance Criteria:**
- ✅ Excel import supports 1000+ medicines
- ✅ Auto-complete displays within 200ms
- ✅ Changes persist across application restarts

---

### FR-007: Test Execution & Reporting

**Requirement:** The system shall execute automated tests and generate reports.

**Test Types:**
- Unit tests (logic validation)
- Snapshot tests (rendering consistency)
- Multi-DPI tests (DPI scaling validation)

**Report Format:**
- Structured log file on Desktop
- Filename: `HomeoLabel_TestReport_yyyy-MM-dd_HH-mm.log`
- Sections: Environment, Test Results, DPI Analysis, Suggestions, Decision

**Acceptance Criteria:**
- ✅ Tests execute via `dotnet test`
- ✅ Report generated within 30 seconds
- ✅ Report includes GO/CONDITIONAL GO/NO-GO decision

---

### FR-008: Manual Validation Workflow

**Requirement:** The system shall support manual validation of physical printer output.

**Validation Levels:**
- 100% Windows scaling (96 DPI)
- 125% Windows scaling (120 DPI)
- 150% Windows scaling (144 DPI)

**Documentation:**
- Manual validation results stored in `Manual_DPI_Test_Results.md`
- Includes tester name, date, printer model, pass/fail status

**Acceptance Criteria:**
- ✅ Manual validation guide exists (MULTI_DPI_TESTING_GUIDE.md)
- ✅ Validation results parseable by test report generator
- ✅ Release blocked if manual validation incomplete

---

## 3. Non-Functional Requirements

### NFR-001: Performance

**Requirement:** The system shall meet performance thresholds for responsiveness.

| Operation | Threshold | Measurement |
|-----------|-----------|-------------|
| Label Rendering | < 50ms | DEBUG instrumentation |
| PDF Export | < 200ms | DEBUG instrumentation |
| UI Thread Responsiveness | < 100ms stalls | UI Thread Watchdog |
| Application Startup | < 5 seconds | User perception |

**Acceptance Criteria:**
- ✅ 95% of operations within threshold
- ✅ Performance regressions detected (> 20% slowdown)
- ✅ Performance logs available in DEBUG builds

---

### NFR-002: Reliability

**Requirement:** The system shall operate reliably under normal conditions.

**Metrics:**
- **Uptime:** > 99% during business hours
- **Crash Rate:** < 1 per 1000 operations
- **Data Loss:** Zero tolerance (all inputs persisted)

**Error Handling:**
- Graceful degradation (print failure doesn't crash app)
- User-friendly error messages (no stack traces)
- Automatic error logging (%LOCALAPPDATA%/Logs/)

**Acceptance Criteria:**
- ✅ Unhandled exceptions caught and logged
- ✅ User notified of errors without data loss
- ✅ Application restarts cleanly after crash

---

### NFR-003: DPI Correctness

**Requirement:** The system shall maintain DPI correctness guarantees.

**Guarantees:**
- **Physical Size Invariance:** 50mm × 30mm ±0.1mm across all DPI levels
- **Pixel Dimension Accuracy:** Calculated dimensions match actual ±1 pixel
- **Baseline Consistency:** Snapshot tests < 0.1% pixel difference

**Validation:**
- Automated multi-DPI tests
- Manual physical measurement (ruler)
- Snapshot baseline comparison

**Acceptance Criteria:**
- ✅ All DPI tests pass
- ✅ Physical measurements within tolerance
- ✅ Baselines validated on physical printer

---

### NFR-004: Print Accuracy

**Requirement:** The system shall ensure print accuracy guarantees.

**Guarantee:** **Preview = PDF = Printed Output**

**Validation:**
| Comparison | Metric | Threshold |
|------------|--------|-----------|
| Preview vs PDF | Pixel difference | < 0.1% |
| Preview vs Print | Visual inspection | 100% match |
| PDF vs Print | Visual inspection | 100% match |

**Acceptance Criteria:**
- ✅ Automated snapshot tests pass
- ✅ Manual validation confirms match
- ✅ No user reports of mismatch

---

### NFR-005: Usability

**Requirement:** The system shall be usable by non-technical shop staff.

**Usability Goals:**
- **Learning Time:** < 30 minutes for basic operation
- **Task Completion:** < 2 minutes per label (input → print)
- **Error Recovery:** Clear error messages, < 1 minute to resolve

**Accessibility:**
- Font size readable at 1920×1080
- Color contrast ratio > 4.5:1
- Keyboard shortcuts for common actions

**Acceptance Criteria:**
- ✅ User training completed in < 30 minutes
- ✅ Users can print labels independently
- ✅ Error messages tested for clarity

---

### NFR-006: Maintainability

**Requirement:** The system shall be maintainable by development team.

**Code Quality:**
- **Unit Test Coverage:** > 80% for business logic
- **Code Documentation:** All public APIs documented
- **Architectural Documentation:** Up-to-date architecture docs

**Change Management:**
- Git version control
- Pull request reviews
- Automated build validation

**Acceptance Criteria:**
- ✅ New developers onboarded in < 1 week
- ✅ Bug fixes deployed in < 3 days
- ✅ Feature additions estimated accurately

---

### NFR-007: Security

**Requirement:** The system shall implement basic security measures.

**Security Considerations:**
- **Data Storage:** Local JSON files (no cloud sync)
- **Printer Access:** Standard Windows printer drivers
- **User Data:** Medicine names only (no PII)

**Threats (Low Risk):**
- Unauthorized access to medicine database (mitigated: local storage)
- Printer hijacking (mitigated: standard Windows security)

**Acceptance Criteria:**
- ✅ No sensitive data stored
- ✅ Standard Windows file permissions
- ✅ No network communication (standalone app)

---

## 4. Hardware & Environment Requirements

### 4.1 Client Machine Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **OS** | Windows 10 21H2 | Windows 11 22H2 |
| **CPU** | Dual-core 2.0 GHz | Quad-core 2.5 GHz |
| **RAM** | 4 GB | 8 GB |
| **Storage** | 100 MB free | 500 MB free |
| **Display** | 1920×1080, 96 DPI | 1920×1080, any DPI |
| **.NET** | .NET 8 Desktop Runtime | .NET 8 SDK (for dev) |

### 4.2 Printer Requirements

| Requirement | Specification |
|-------------|---------------|
| **Type** | Thermal label printer |
| **DPI** | 203 DPI (standard) |
| **Media Size** | 50mm × 30mm (or custom size support) |
| **Interface** | USB or Network |
| **Driver** | Windows-compatible driver installed |
| **Tested Models** | SNBC TVSE LP 46 NEO BPLE (primary) |

### 4.3 Development Environment

| Tool | Version | Purpose |
|------|---------|---------|
| Visual Studio | 2022 or later | IDE |
| .NET SDK | 8.0 or later | Build & test |
| Git | 2.40+ | Version control |
| Windows | 10/11 | Target platform |

---

## 5. User Roles & Responsibilities

### 5.1 End User (Shop Staff)

**Responsibilities:**
- Enter medicine information accurately
- Verify label preview before printing
- Print labels to correct printer
- Report errors or mismatches

**Training Required:**
- Basic Windows operation
- Application navigation (< 30 minutes)
- Error handling procedures

### 5.2 QA Engineer

**Responsibilities:**
- Execute automated test suite
- Perform manual DPI validation (3 scaling levels)
- Document test results
- Approve or block releases

**Skills Required:**
- Understanding of DPI concepts
- Ability to change Windows display scaling
- Physical printer access
- Markdown documentation

### 5.3 System Administrator

**Responsibilities:**
- Install .NET 8 Desktop Runtime
- Install and configure thermal printer drivers
- Deploy application updates
- Troubleshoot printer issues

**Skills Required:**
- Windows system administration
- Printer driver installation
- Basic networking (for network printers)

### 5.4 Developer

**Responsibilities:**
- Implement features per requirements
- Write unit tests (> 80% coverage)
- Fix bugs within SLA (< 3 days)
- Maintain documentation

**Skills Required:**
- C# / .NET proficiency
- WPF framework knowledge
- xUnit testing
- Git version control

---

## 6. Error Handling & Recovery

### 6.1 Error Categories

| Category | Examples | Handling |
|----------|----------|----------|
| **User Input** | Empty required field | Validation message, prevent action |
| **Printer Offline** | Printer not connected | Error dialog, retry option |
| **PDF Export Fail** | Disk full | Error message, alternate location |
| **Rendering Fail** | Out of memory | Graceful degradation, log error |
| **File I/O** | Permission denied | Retry with admin, log error |

### 6.2 Error Message Standards

**Format:** `[Error Code] - [User-Friendly Message] - [Action Required]`

**Example:**
```
PRINT-001 - Printer not found
The selected printer is offline or not connected.
Please check the printer connection and try again.
```

### 6.3 Recovery Procedures

| Error | Recovery Steps |
|-------|----------------|
| **Application Crash** | 1. Auto-restart, 2. Restore last input, 3. Log stack trace |
| **Printer Error** | 1. Show error, 2. Offer retry, 3. Suggest printer check |
| **PDF Export Fail** | 1. Show error, 2. Offer alternate location, 3. Log details |

---

## 7. Validation & Acceptance Criteria

### 7.1 Release Validation Checklist

**Pre-Release Gates:**

| Gate | Requirement | Validation Method |
|------|-------------|-------------------|
| **Unit Tests** | 100% pass | Automated (`dotnet test`) |
| **Snapshot Tests** | 100% pass OR baselines validated | Automated + manual printer check |
| **Multi-DPI Tests** | 100% pass at 96, 120, 144 DPI | Automated |
| **Manual Validation** | PASS at 100%, 125%, 150% scaling | Manual testing on printer |
| **Performance** | Within thresholds (< 50ms, < 200ms) | DEBUG instrumentation |
| **Test Report** | Generated and archived | Automated |

**Release Decision:**
- **GO:** All gates pass
- **CONDITIONAL GO:** Minor failures, manual validation pending
- **NO-GO:** Unit test failures or manual validation failures

### 7.2 User Acceptance Testing (UAT)

**UAT Criteria:**

| Test Case | Expected Result | Pass/Fail |
|-----------|-----------------|-----------|
| TC-001: Enter medicine info and print | Label prints correctly | ☐ |
| TC-002: Preview matches print | Visual match confirmed | ☐ |
| TC-003: Export to PDF | PDF viewable and printable | ☐ |
| TC-004: Change Windows scaling to 125% | App functions correctly | ☐ |
| TC-005: Print 10 labels consecutively | All labels identical | ☐ |

**UAT Sign-Off:**
- Business owner signature required
- QA lead signature required
- Date of approval documented

---

## 8. Compliance & Standards

### 8.1 Quality Standards

**ISO 9001 Alignment (Quality Management):**
- Document control (versioned documentation)
- Process consistency (automated tests)
- Continuous improvement (performance monitoring)

**Audit Trail:**
- Test reports archived (Desktop)
- Manual validation documented (Markdown files)
- Session logs retained (30 days)

### 8.2 Testing Standards

**Test Pyramid (Industry Standard):**
```
     Manual
    /       \
   System    
  /         \
 Rendering   
/           \
Unit Tests  
```

**Coverage Targets:**
- Unit Tests: > 80%
- Integration Tests: > 60%
- Manual Validation: 100%

---

## 9. Assumptions & Dependencies

### 9.1 Assumptions

1. Users have Windows 10/11 with .NET 8 Runtime
2. Thermal printer drivers pre-installed
3. Users trained on Windows display scaling
4. Shop has stable power supply (no mid-print outages)
5. Medicine database manually maintained (no auto-sync)

### 9.2 Dependencies

| Dependency | Impact if Unavailable |
|------------|----------------------|
| .NET 8 Runtime | Application won't launch |
| Printer Driver | Printing disabled |
| Segoe UI Font | Text rendering may differ |
| Windows DWM | UI rendering may degrade |

---

## 10. Future Enhancements (Out of Scope for v1.0)

**Potential Future Features:**
- Cloud backup of medicine database
- Multi-user support (network printing)
- Barcode generation (QR codes for tracking)
- Mobile app for remote printing
- Linux/macOS support (via Avalonia framework)

**Not Planned:**
- Inventory management
- Billing integration
- Multi-language support

---

## 11. Glossary

| Term | Definition |
|------|------------|
| **DIP** | Device Independent Pixel (WPF layout unit, 1 DIP = 1/96 inch) |
| **DPI** | Dots Per Inch (display or printer resolution) |
| **Snapshot Test** | Automated test comparing rendered output to baseline image |
| **Baseline** | Reference image for snapshot comparison |
| **Physical Size Invariance** | Property ensuring physical output size constant across DPI levels |
| **Thermal Printer** | Printer using heat to create images on heat-sensitive paper |
| **TSPL** | TSC Printer Language (command set for label printers) |

---

## 12. Document Control

**Document Owner:** Development Team  
**Review Cycle:** Quarterly or per major release  
**Last Updated:** January 2024  

**Approval Signatures:**

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Project Manager | _____________ | _____________ | ______ |
| Lead Developer | _____________ | _____________ | ______ |
| QA Lead | _____________ | _____________ | ______ |
| Business Owner | _____________ | _____________ | ______ |

**Version History:**

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | Jan 2024 | Initial requirements specification | Requirements Analyst |

---

**END OF DOCUMENT**
