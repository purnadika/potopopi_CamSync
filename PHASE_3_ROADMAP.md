# 🗺️ PHASE 3: ROADMAP & IMPLEMENTATION PLAN

**Current Status**: Phase 2 Complete ✅ (Refactoring + Core Engine)  
**Next Phase**: Phase 3 (Resilience, Features, Polish)  
**Framework**: .NET 10  

---

## 📊 Phase 3 Priorities

### Priority 1: **RESILIENCE & SAFETY** 🛡️ (Critical)
Impact: **High** | Effort: **Medium** | Timeline: **1-2 weeks**

- [ ] **Disk Space Validation** (Quick - 15 min)
- [ ] **Partial Download Recovery** (Medium - 1-2 hours)
- [ ] **Exponential Backoff Retry** (Medium - 1-2 hours)

### Priority 2: **PERFORMANCE** ⚡ (High Value)
Impact: **Medium** | Effort: **Medium** | Timeline: **1 week**

- [ ] **Parallel Pipeline** (Download + Upload simultaneously)
- [ ] **Progress Streaming** (Real-time metrics)
- [ ] **Throttling & Rate Limiting** (Network friendly)

### Priority 3: **FEATURES** 🎁 (High Value)
Impact: **Medium** | Effort: **Medium** | Timeline: **2 weeks**

- [ ] **Smart Cleanup** (Delete after sync toggle)
- [ ] **Exclusion Filters** (Glob/Regex patterns)
- [ ] **Immich Albums** (Per-device album assignment)

### Priority 4: **UI/UX POLISH** ✨ (Nice to Have)
Impact: **Low** | Effort: **High** | Timeline: **2-3 weeks**

- [ ] **Modern Typography** (Outfit/Inter fonts)
- [ ] **Glassmorphism** (Dark mode UI)
- [ ] **Animations** (Shimmer, transitions)

### Priority 5: **DISTRIBUTION** 📦 (Nice to Have)
Impact: **Low** | Effort: **Medium** | Timeline: **1 week**

- [ ] **Single-File Build** (PublishSingleFile)
- [ ] **Auto-Update Checker** (GitHub releases)

---

## 🎯 Recommended Execution Order

### **Week 1: Foundation & Safety** 🛡️
1. ✅ Disk Space Validation
2. ✅ Partial Download Recovery
3. ✅ Exponential Backoff

### **Week 2: Performance** ⚡
4. ✅ Parallel Pipeline Architecture
5. ✅ Progress Streaming
6. ✅ Throttling

### **Week 3-4: Features** 🎁
7. ✅ Smart Cleanup
8. ✅ Exclusion Filters
9. ✅ Immich Albums

### **Week 5-6: Polish & Distribution** ✨📦
10. ✅ UI/UX Enhancements
11. ✅ Single-File Build
12. ✅ Auto-Update

---

## 🚀 Starting NOW: Phase 3.1 - RESILIENCE & SAFETY

### Implementation 1: Disk Space Validation ⏱️ 15 min

**What**: Check target drive has enough space before syncing

**Where**: `SyncOrchestrator.cs` - StartSyncAsync method

**Why**: Prevent failed syncs due to full disks

**How**:
```csharp
private bool ValidateDiskSpace(string targetPath, long requiredBytes)
{
    var drive = new DriveInfo(Path.GetPathRoot(targetPath)!);
    if (drive.AvailableFreeSpace < requiredBytes)
    {
        Progress($"Insufficient disk space. Need {FormatBytes(requiredBytes)}, Have {FormatBytes(drive.AvailableFreeSpace)}");
        return false;
    }
    return true;
}
```

---

### Implementation 2: Partial Download Recovery 1-2 hours

**What**: Download to `.tmp` file, rename on 100% completion

**Where**: `IDeviceProvider` implementations (MTP, SD Card)

**Why**: Prevent corrupt files if sync is interrupted

**How**:
```csharp
// Download to .tmp first
string tempPath = localPath + ".tmp";
using (var fs = new FileStream(tempPath, FileMode.Create, ...))
{
    await device.DownloadToStreamAsync(file, fs, cancellationToken);
}
// Only rename on success
File.Move(tempPath, localPath, overwrite: true);
```

---

### Implementation 3: Exponential Backoff ⏱️ 1-2 hours

**What**: Retry failed Immich uploads with increasing delays

**Where**: `ImmichSync.cs` - UploadAsync method

**Why**: Handle network blips gracefully

**How**: Use `Polly` library for resilience patterns

```csharp
var policy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s
        onRetry: (outcome, timespan) => 
            _logger.LogWarning($"Retrying in {timespan.TotalSeconds}s...")
    );
```

---

## 📋 Quick Decision: What Should We Build First?

Pick your preference:

**Option A** (Safest First): All three resilience features
- Best: Safety-focused, no UI changes, fully testable
- Time: ~3-4 hours
- Impact: Prevents data loss, improves reliability

**Option B** (Quick Win): Just Disk Space Validation
- Best: 15 minutes, immediate impact
- Then: Layer in others progressively
- Impact: Quick fix for common issue

**Option C** (High Impact): Parallel Pipeline
- Best: Massive performance improvement
- Time: ~3-4 hours
- Impact: Speeds up sync 2-3x

**Option D** (Smart): Validation + Recovery + Quick UI
- Best: Balanced approach
- Time: ~2-3 hours
- Impact: Safe + better UX

---

## 📊 Impact vs Effort Matrix

```
           HIGH IMPACT
                ↑
                │   Parallel Pipeline ⚡
                │   Exponential Backoff 🔄
                │
         Disk Space ✓
        Validation   │
                │   Smart Cleanup 🗑️
                │
         Animations │
           Fonts     │ Exclusion Filters
                │   Albums
                │
                └──────────────────→
                    LOW         HIGH
                    EFFORT
```

---

## 🎯 My Recommendation

**Start with: Resilience & Safety (All 3)**

Why:
1. **No UI changes** = Lower risk of breaking things
2. **All 3 are critical** for a backup app
3. **Fully testable** = Can add tests immediately
4. **Foundation** = Makes next features safer
5. **Time efficient** = ~3-4 hours for major safety gains

**Then**: Performance (Parallel Pipeline) in Week 2

---

## ✅ Next: Choose Your Path

Reply with which you'd like to tackle:

1. **SAFETY FIRST** 🛡️ - All 3 resilience features
2. **QUICK WIN** ⚡ - Just disk space validation first
3. **PERFORMANCE** 🚀 - Parallel pipeline immediately
4. **BALANCED** ⚖️ - Validation + Recovery + small UI tweak

I'm ready to implement whichever you choose! 🚀

---

*Which direction should we go?*
