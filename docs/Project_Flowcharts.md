# HomeoMahanagarLabelCleanV2 - Project Flowcharts

**Document Version:** 1.0  
**Date:** January 2024  
**Document Type:** Visual Process Documentation  
**Format:** Mermaid Diagrams + ASCII Flowcharts  

---

## Introduction

This document provides visual flowcharts for all major processes in the HomeoMahanagarLabelCleanV2 system. Flowcharts use Mermaid diagram syntax (renderable in GitHub, VS Code, and documentation tools).

**Flowcharts Included:**
1. High-Level System Flowchart
2. Application Startup Flowchart
3. User Interaction Flowchart
4. Rendering Pipeline Flowchart
5. Print Execution Flowchart
6. Test Execution Flowchart
7. Release Decision Flowchart

---

## Flowchart 1: High-Level System Overview

### Mermaid Diagram

```mermaid
graph TB
    Start([User Launches App]) --> Init[Application Initialization]
    Init --> MainUI[Display Main Window]
    
    MainUI --> Input{User Action?}
    
    Input -->|Enter Data| UpdatePreview[Update Preview]
    UpdatePreview --> Input
    
    Input -->|Export PDF| PDFFlow[PDF Export Flow]
    PDFFlow --> PDFSaved[PDF Saved to Disk]
    PDFSaved --> Input
    
    Input -->|Print Label| PrintFlow[Print Execution Flow]
    PrintFlow --> PrintDone[Label Printed]
    PrintDone --> Input
    
    Input -->|Exit| Shutdown[Application Shutdown]
    Shutdown --> End([Exit])
    
    style Start fill:#90EE90
    style End fill:#FFB6C1
    style UpdatePreview fill:#87CEEB
    style PDFFlow fill:#FFD700
    style PrintFlow fill:#FFA500
```

### Description

**Flow:** User launches app → Main window displayed → User performs actions (enter data, export PDF, print, or exit)

**Key Decision Points:**
- User action determines next flow (preview update, PDF export, print, or exit)

---

## Flowchart 2: Application Startup Flow

### Mermaid Diagram

```mermaid
flowchart TD
    Start([App Launched]) --> LoadRuntime[.NET 8 Runtime Initializes]
    LoadRuntime --> CheckDebug{DEBUG Build?}
    
    CheckDebug -->|Yes| InitLogs[Initialize Logging]
    InitLogs --> InitSession[Start Session Logger]
    InitSession --> StartWatchdog[Start UI Thread Watchdog]
    StartWatchdog --> RegisterHandlers[Register Error Handlers]
    
    CheckDebug -->|No| RegisterHandlers
    
    RegisterHandlers --> CheckFirstRun{First Run?}
    CheckFirstRun -->|Yes| SeedDB[Import remedies.xlsx]
    SeedDB --> LoadMain[Load Main Window]
    
    CheckFirstRun -->|No| LoadMain
    
    LoadMain --> BindVM[Bind LabelViewModel]
    BindVM --> RestoreState[Restore Last State]
    RestoreState --> ShowUI[Display Main Window]
    ShowUI --> Ready([App Ready])
    
    style Start fill:#90EE90
    style Ready fill:#90EE90
    style CheckDebug fill:#FFD700
    style CheckFirstRun fill:#FFD700
```

### Description

**Conditional Flows:**
- **DEBUG Build:** Extra logging and monitoring enabled
- **Release Build:** Minimal overhead, direct to main window
- **First Run:** Import medicine database from Excel

**Duration:** < 5 seconds

---

## Flowchart 3: User Interaction Flow (Input → Preview → Print)

### Mermaid Diagram

```mermaid
flowchart TD
    Start([User Opens App]) --> EnterName[Enter Medicine Name]
    EnterName --> Autocomplete{Autocomplete Match?}
    
    Autocomplete -->|Yes| ShowSuggestions[Show Dropdown]
    ShowSuggestions --> SelectMedicine[User Selects]
    SelectMedicine --> EnterPotency
    
    Autocomplete -->|No| EnterPotency[Enter Potency]
    
    EnterPotency --> EnterDosage[Enter Dosage]
    EnterDosage --> EnterTiming[Enter Timing Optional]
    EnterTiming --> SelectShop[Select Shop Name]
    
    SelectShop --> UpdatePreview[Preview Updates Real-Time]
    UpdatePreview --> ValidateInputs[Validate All Fields]
    
    ValidateInputs --> UserAction{User Clicks?}
    
    UserAction -->|Export PDF| CheckValid1{All Required Fields?}
    CheckValid1 -->|No| ShowError1[Show Error Message]
    ShowError1 --> UserAction
    CheckValid1 -->|Yes| ExportPDF[PDF Export Flow]
    ExportPDF --> PDFSaved[PDF Saved]
    PDFSaved --> UserAction
    
    UserAction -->|Print| CheckValid2{All Required Fields?}
    CheckValid2 -->|No| ShowError2[Show Error Message]
    ShowError2 --> UserAction
    CheckValid2 -->|Yes| PrintLabel[Print Execution Flow]
    PrintLabel --> PrintDone[Label Printed]
    PrintDone --> UserAction
    
    UserAction -->|Edit More| EnterName
    UserAction -->|Exit| End([Exit])
    
    style Start fill:#90EE90
    style End fill:#FFB6C1
    style UpdatePreview fill:#87CEEB
    style CheckValid1 fill:#FFD700
    style CheckValid2 fill:#FFD700
```

### Description

**Real-Time Preview:** Every input change triggers preview update (< 100ms)

**Validation Gates:** PDF export and print require all mandatory fields filled

---

## Flowchart 4: Rendering Pipeline Flow (WPF → DPI → Bitmap)

### Mermaid Diagram

```mermaid
flowchart TB
    Start([Render Trigger]) --> GetSize[Calculate Logical Size]
    GetSize --> Note1[50mm × 30mm<br/>= 189 DIP × 113 DIP]
    
    Note1 --> CreateView[Create PrintLabelView]
    CreateView --> PopulateItems[Call RenderItems]
    PopulateItems --> ClearCanvas[Clear Canvas]
    ClearCanvas --> CreateTextBlocks[Create TextBlock for Each Item]
    CreateTextBlocks --> PositionElements[Position & Scale Elements]
    
    PositionElements --> MeasureArrange[WPF Layout Pass:<br/>Measure → Arrange → UpdateLayout]
    
    MeasureArrange --> GetTargetDPI[Get Target DPI]
    GetTargetDPI --> CalculatePixels[Calculate Pixel Dimensions<br/>pixels = DIPs × DPI/96]
    
    CalculatePixels --> CreateBitmap[Create RenderTargetBitmap<br/>at Target DPI]
    CreateBitmap --> RenderToBitmap[Render WPF Visual to Bitmap]
    
    RenderToBitmap --> EncodePNG[Encode to PNG<br/>PngBitmapEncoder]
    EncodePNG --> PNGBytes[PNG Byte Array]
    
    PNGBytes --> UseCase{Destination?}
    
    UseCase -->|PDF Export| EmbedPDF[Embed PNG in PDF]
    EmbedPDF --> SavePDF[Save PDF File]
    SavePDF --> Done1([PDF Complete])
    
    UseCase -->|Print| SendPrinter[Send PNG to Printer Driver]
    SendPrinter --> PrinterProcess[Printer Converts & Prints]
    PrinterProcess --> Done2([Print Complete])
    
    style Start fill:#90EE90
    style Done1 fill:#90EE90
    style Done2 fill:#90EE90
    style MeasureArrange fill:#87CEEB
    style RenderToBitmap fill:#FFD700
```

### Description

**Key Insight:** WPF visual remains DPI-independent. Only the final bitmap rasterization depends on target DPI (300 for PDF/print, varies for screen display).

**Critical Path:** Measure → Arrange → UpdateLayout **MUST** complete before rendering, or positions will be incorrect.

---

## Flowchart 5: Print Execution Flow

### Mermaid Diagram

```mermaid
flowchart TD
    Start([Print Button Clicked]) --> Validate{All Fields Filled?}
    
    Validate -->|No| ShowError[Display Error Message]
    ShowError --> End([Cancel])
    
    Validate -->|Yes| GetPrinter[Get Selected Printer]
    GetPrinter --> CheckStatus{Printer Online?}
    
    CheckStatus -->|No| ShowOffline[Show Printer Offline Error]
    ShowOffline --> End
    
    CheckStatus -->|Yes| RenderLabel[Render Label to PNG<br/>at 300 DPI]
    RenderLabel --> CreateJob[Create Print Job]
    CreateJob --> QueueJob[Add to Print Queue]
    
    QueueJob --> ExecuteJob[Execute Print Job]
    ExecuteJob --> SendToPrinter[Send PNG to Printer Driver]
    SendToPrinter --> DriverProcess[Driver Converts to TSPL]
    DriverProcess --> PrinterPrint[Thermal Printer Prints]
    
    PrinterPrint --> WaitComplete{Print Complete?}
    
    WaitComplete -->|Timeout| ShowTimeout[Show Timeout Error<br/>Offer Retry]
    ShowTimeout --> RetryChoice{Retry?}
    RetryChoice -->|Yes| ExecuteJob
    RetryChoice -->|No| End
    
    WaitComplete -->|Success| RemoveJob[Remove from Queue]
    RemoveJob --> Notify[Show Success Message]
    Notify --> Success([Print Complete])
    
    style Start fill:#90EE90
    style Success fill:#90EE90
    style End fill:#FFB6C1
    style Validate fill:#FFD700
    style CheckStatus fill:#FFD700
    style WaitComplete fill:#FFD700
```

### Description

**Error Handling:** Multiple validation gates (fields, printer status, completion)

**Retry Logic:** Timeout allows user to retry print operation

**Duration:** 2-5 seconds (typical)

---

## Flowchart 6: Test Execution Flow

### Mermaid Diagram

```mermaid
flowchart TB
    Start([Run Test Suite]) --> CollectEnv[Collect Environment Info<br/>OS, DPI, Machine Name]
    CollectEnv --> RunUnit[Execute Unit Tests<br/>Category=Unit]
    
    RunUnit --> UnitResult{All Pass?}
    UnitResult -->|No| UnitFail[Record Failures]
    UnitFail --> DecisionNoGo[Decision: NO-GO]
    DecisionNoGo --> GenerateReport
    
    UnitResult -->|Yes| RunSnapshot[Execute Snapshot Tests<br/>Category=Snapshot]
    RunSnapshot --> SnapshotResult{All Pass?}
    
    SnapshotResult -->|No| RecordSnapshotFail[Record Baseline Mismatches]
    SnapshotResult -->|Yes| RecordSnapshotPass[Record Pass]
    
    RecordSnapshotFail --> RunDPI
    RecordSnapshotPass --> RunDPI[Execute Multi-DPI Tests<br/>Category=DPI]
    
    RunDPI --> DPILoop[For Each DPI 96, 120, 144]
    DPILoop --> RenderAtDPI[Render at Target DPI]
    RenderAtDPI --> ValidateDimensions[Validate Pixel Dimensions]
    ValidateDimensions --> ValidatePhysical[Validate Physical Size]
    ValidatePhysical --> CompareBaseline[Compare to Baseline]
    CompareBaseline --> NextDPI{More DPI Levels?}
    
    NextDPI -->|Yes| DPILoop
    NextDPI -->|No| CheckPerf[Collect Performance Metrics]
    
    CheckPerf --> CheckManual[Check Manual Validation File]
    CheckManual --> ManualExists{File Exists?}
    
    ManualExists -->|Yes| ParseManual[Parse Validation Results]
    ParseManual --> ManualComplete{All Levels PASS?}
    ManualComplete -->|Yes| AllPassManual[Manual Validation Complete]
    ManualComplete -->|No| ManualPending[Manual Validation Partial/Failed]
    
    ManualExists -->|No| ManualPending
    
    AllPassManual --> AnalyzeResults
    ManualPending --> AnalyzeResults[Analyze All Results]
    
    AnalyzeResults --> DetermineDecision{Decision Logic}
    
    DetermineDecision -->|All Tests Pass + Manual Complete| DecisionGo[Decision: GO]
    DetermineDecision -->|Tests Pass, Manual Pending| DecisionConditional[Decision: CONDITIONAL GO]
    DetermineDecision -->|Rendering Fail, Manual Incomplete| DecisionConditional
    
    DecisionGo --> GenerateReport[Generate Test Report]
    DecisionConditional --> GenerateReport
    
    GenerateReport --> SaveDesktop[Save to Desktop<br/>HomeoLabel_TestReport_*.log]
    SaveDesktop --> PrintSummary[Print Console Summary]
    PrintSummary --> End([Test Complete])
    
    style Start fill:#90EE90
    style End fill:#90EE90
    style UnitResult fill:#FFD700
    style SnapshotResult fill:#FFD700
    style ManualExists fill:#FFD700
    style ManualComplete fill:#FFD700
    style DetermineDecision fill:#FF6347
    style DecisionGo fill:#32CD32
    style DecisionConditional fill:#FFA500
    style DecisionNoGo fill:#DC143C
```

### Description

**Decision Gates:**
- **Unit Test Failure:** Immediate NO-GO (critical logic broken)
- **Rendering Failures:** CONDITIONAL GO (requires manual validation)
- **All Pass + Manual Complete:** GO (ready for release)

**Duration:** 8-15 seconds

---

## Flowchart 7: Release Decision Flowchart (GO / CONDITIONAL GO / NO-GO)

### Mermaid Diagram

```mermaid
flowchart TD
    Start([Test Suite Complete]) --> CheckUnit{Unit Tests<br/>100% Pass?}
    
    CheckUnit -->|No| NoGoUnit[NO-GO:<br/>Critical Logic Failure]
    NoGoUnit --> SuggestionUnit[Suggestion: Fix unit tests<br/>immediately BLOCKING]
    SuggestionUnit --> BlockRelease[BLOCK RELEASE]
    BlockRelease --> End1([Exit Code: 1])
    
    CheckUnit -->|Yes| CheckManualFail{Manual Validation<br/>Status = FAIL?}
    
    CheckManualFail -->|Yes| NoGoManual[NO-GO:<br/>Printed Output Incorrect]
    NoGoManual --> SuggestionManual[Suggestion: Investigate<br/>rendering bug on printer]
    SuggestionManual --> BlockRelease
    
    CheckManualFail -->|No| CheckSnapshot{Snapshot Tests<br/>100% Pass?}
    
    CheckSnapshot -->|No| RecordSnapshotIssue[Record Snapshot Failures]
    CheckSnapshot -->|Yes| RecordSnapshotOK[Record Snapshot Pass]
    
    RecordSnapshotIssue --> CheckDPI
    RecordSnapshotOK --> CheckDPI{Multi-DPI Tests<br/>100% Pass?}
    
    CheckDPI -->|No| RecordDPIIssue[Record DPI Failures]
    CheckDPI -->|Yes| RecordDPIOK[Record DPI Pass]
    
    RecordDPIIssue --> CheckManualComplete
    RecordDPIOK --> CheckManualComplete{Manual Validation<br/>Status = COMPLETE?}
    
    CheckManualComplete -->|Yes, All Levels PASS| AllGood[All Automated + Manual PASS]
    AllGood --> DecisionGo[Decision: GO<br/>Ready for Release]
    DecisionGo --> SuggestionGo[Suggestion: Deploy to production]
    SuggestionGo --> ApproveRelease[APPROVE RELEASE]
    ApproveRelease --> End2([Exit Code: 0])
    
    CheckManualComplete -->|No Manual Validation| ManualNotDone[Manual Validation NOT COMPLETE]
    ManualNotDone --> ConditionalManual[Decision: CONDITIONAL GO<br/>Manual Validation Required]
    ConditionalManual --> SuggestionConditional1[Suggestions:<br/>1. Test at 100% 125% 150% scaling<br/>2. Print on physical printer<br/>3. Document results<br/>4. Re-run test suite]
    SuggestionConditional1 --> RequireManual[REQUIRE MANUAL VALIDATION]
    RequireManual --> End3([Exit Code: 0])
    
    CheckManualComplete -->|Partial PASS| ManualPartial[Some Manual Tests PASS,<br/>Some NOT VERIFIED]
    ManualPartial --> ConditionalManual
    
    RecordSnapshotIssue --> ConditionalSnapshot[Decision: CONDITIONAL GO<br/>Baseline Mismatch]
    RecordDPIIssue --> ConditionalDPI[Decision: CONDITIONAL GO<br/>DPI Rendering Issue]
    
    ConditionalSnapshot --> SuggestionConditional2[Suggestions:<br/>1. Delete stale baseline<br/>2. Re-run test to create new<br/>3. Print new baseline on printer<br/>4. If correct commit new baseline]
    SuggestionConditional2 --> RequireValidation[REQUIRE BASELINE VALIDATION]
    RequireValidation --> End3
    
    ConditionalDPI --> SuggestionConditional3[Suggestions:<br/>1. Set Windows scaling to failed level<br/>2. Test app manually<br/>3. Print on printer<br/>4. Update baseline if correct]
    SuggestionConditional3 --> RequireValidation
    
    style Start fill:#90EE90
    style End1 fill:#DC143C
    style End2 fill:#32CD32
    style End3 fill:#FFA500
    style CheckUnit fill:#FFD700
    style CheckManualFail fill:#FFD700
    style CheckSnapshot fill:#FFD700
    style CheckDPI fill:#FFD700
    style CheckManualComplete fill:#FFD700
    style DecisionGo fill:#32CD32
    style ConditionalManual fill:#FFA500
    style ConditionalSnapshot fill:#FFA500
    style ConditionalDPI fill:#FFA500
    style NoGoUnit fill:#DC143C
    style NoGoManual fill:#DC143C
```

### Description

**Decision Logic:**

1. **NO-GO (Red):** Critical failures
   - Unit tests failed
   - Manual validation failed (printed output wrong)
   - **Action:** Block release, fix issues

2. **GO (Green):** All gates passed
   - All automated tests passed
   - Manual validation complete and passed
   - **Action:** Approve release

3. **CONDITIONAL GO (Orange):** Manual intervention needed
   - Tests passed but manual validation pending
   - Rendering failures (snapshot/DPI) requiring physical printer validation
   - **Action:** Complete manual validation before release

---

## Flowchart 8: Manual Validation Workflow

### Mermaid Diagram

```mermaid
flowchart TD
    Start([CONDITIONAL GO Decision]) --> ReadGuide[Read MULTI_DPI_TESTING_GUIDE.md]
    ReadGuide --> SetScaling100[Set Windows Scaling to 100%]
    
    SetScaling100 --> Logout1[Log Out & Log Back In]
    Logout1 --> LaunchApp1[Launch Application]
    LaunchApp1 --> EnterTest1[Enter Standard Test Label:<br/>ARNICA MONTANA, 200 CH, etc.]
    
    EnterTest1 --> Preview1[Check Preview]
    Preview1 --> ExportPDF1[Export to PDF]
    ExportPDF1 --> PrintPDF1[Print PDF from Viewer]
    PrintPDF1 --> PrintDirect1[Print Directly from App]
    
    PrintDirect1 --> Validate1{All 3 Outputs Match?}
    Validate1 -->|No| Fail1[Document FAIL<br/>Investigate Issue]
    Fail1 --> NoGo[Change Decision to NO-GO]
    NoGo --> EndFail([Block Release])
    
    Validate1 -->|Yes| Document1[Document PASS in Manual_DPI_Test_Results.md]
    Document1 --> SetScaling125[Set Windows Scaling to 125%]
    
    SetScaling125 --> Logout2[Log Out & Log Back In]
    Logout2 --> LaunchApp2[Launch Application]
    LaunchApp2 --> EnterTest2[Enter Same Test Label]
    EnterTest2 --> Preview2[Check Preview]
    Preview2 --> ExportPDF2[Export to PDF]
    ExportPDF2 --> PrintPDF2[Print PDF from Viewer]
    PrintPDF2 --> PrintDirect2[Print Directly from App]
    
    PrintDirect2 --> Validate2{All 3 Outputs Match?}
    Validate2 -->|No| Fail2[Document FAIL]
    Fail2 --> NoGo
    
    Validate2 -->|Yes| Document2[Document PASS]
    Document2 --> SetScaling150[Set Windows Scaling to 150%]
    
    SetScaling150 --> Logout3[Log Out & Log Back In]
    Logout3 --> LaunchApp3[Launch Application]
    LaunchApp3 --> EnterTest3[Enter Same Test Label]
    EnterTest3 --> Preview3[Check Preview]
    Preview3 --> ExportPDF3[Export to PDF]
    ExportPDF3 --> PrintPDF3[Print PDF from Viewer]
    PrintPDF3 --> PrintDirect3[Print Directly from App]
    
    PrintDirect3 --> Validate3{All 3 Outputs Match?}
    Validate3 -->|No| Fail3[Document FAIL]
    Fail3 --> NoGo
    
    Validate3 -->|Yes| Document3[Document PASS]
    Document3 --> AllComplete[All 3 Levels PASS]
    AllComplete --> RerunTests[Re-Run Test Suite]
    
    RerunTests --> NewDecision[New Decision: GO]
    NewDecision --> EndGo([Approve Release])
    
    style Start fill:#FFA500
    style EndGo fill:#32CD32
    style EndFail fill:#DC143C
    style Validate1 fill:#FFD700
    style Validate2 fill:#FFD700
    style Validate3 fill:#FFD700
    style NoGo fill:#DC143C
    style NewDecision fill:#32CD32
```

### Description

**Critical Steps:**
1. **Log out/log in** after changing Windows scaling (required for DPI changes to take effect)
2. **Test all 3 output paths:** Preview, PDF print, direct print (all must match)
3. **Document results** in Markdown file (parseable by test suite)

**Duration:** 1-2 hours (for all 3 scaling levels)

---

## ASCII Flowchart Summary (High-Level System)

```
┌─────────────────────────────────────────────────┐
│              USER LAUNCHES APP                  │
└───────────────────┬─────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────┐
│         APPLICATION INITIALIZATION              │
│  - Load .NET Runtime                            │
│  - Initialize Logging (DEBUG)                   │
│  - Load Medicine Database                       │
│  - Display Main Window                          │
└───────────────────┬─────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────┐
│          MAIN UI (User Interaction)             │
│                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐     │
│  │  Enter   │  │  Export  │  │  Print   │     │
│  │   Data   │  │   PDF    │  │  Label   │     │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘     │
│       │             │             │             │
└───────┼─────────────┼─────────────┼─────────────┘
        │             │             │
        ▼             ▼             ▼
  ┌─────────┐   ┌─────────┐   ┌─────────┐
  │ Preview │   │   PDF   │   │  Print  │
  │ Updates │   │  Export │   │  Queue  │
  │(Real-Time)  │  Flow   │   │  Flow   │
  └─────────┘   └─────────┘   └─────────┘
                      │             │
                      ▼             ▼
                ┌─────────┐   ┌─────────┐
                │PDF File │   │Physical │
                │ Saved   │   │ Label   │
                └─────────┘   └─────────┘
```

---

## Summary Table: Decision Points & Actions

| Decision Point | Condition | Action |
|----------------|-----------|--------|
| **Unit Test Result** | All pass | Continue to next tests |
|  | Any fail | NO-GO, block release |
| **Snapshot Test Result** | All pass | Mark as validated |
|  | Any fail | CONDITIONAL GO, validate baseline on printer |
| **Multi-DPI Test Result** | All pass | Mark as validated |
|  | Any fail | CONDITIONAL GO, test at specific DPI manually |
| **Manual Validation Status** | All levels PASS | GO, approve release |
|  | Any level FAIL | NO-GO, investigate rendering bug |
|  | Not complete | CONDITIONAL GO, require manual testing |
| **Overall Release Decision** | GO | Deploy to production |
|  | CONDITIONAL GO | Complete manual validation, then re-evaluate |
|  | NO-GO | Fix issues, re-run tests |

---

## Glossary

| Symbol | Meaning | Example |
|--------|---------|---------|
| `([Text])` | Start/End (Rounded) | `([App Launched])` |
| `[Text]` | Process (Rectangle) | `[Load Main Window]` |
| `{Text?}` | Decision (Diamond) | `{All Tests Pass?}` |
| `-->` | Flow Direction | `A --> B` |
| `-->|Label|` | Conditional Flow | `{Pass?} -->|Yes| Next` |
| Green | Success State | `fill:#90EE90` |
| Red | Failure State | `fill:#DC143C` |
| Orange | Warning State | `fill:#FFA500` |
| Yellow | Decision Point | `fill:#FFD700` |

---

## Document Control

**Document Owner:** Technical Documentation Team  
**Review Cycle:** Per major release  
**Last Updated:** January 2024  
**Version:** 1.0  

**Rendering Tools:**
- GitHub Markdown (renders Mermaid)
- VS Code Mermaid Extension
- Mermaid Live Editor: https://mermaid.live

---

**END OF DOCUMENT**
