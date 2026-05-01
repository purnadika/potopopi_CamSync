# 🚀 NEXT STEPS & ACTION ITEMS

**Status**: Phase 3.2 Complete ✅  
**Ready for**: Phase 4 Implementation  
**Recommendation**: Start with Smart Cleanup (Quick Win)

---

## 🎯 Priority Matrix

```
                HIGH IMPACT
                     ↑
                     │
       Smart Cleanup ✓  │  Exclusion Filters
       (2h, ~15KB)      │  (3h, ~20KB)
                     │
       Logging ✓        │  Albums
       (2h, ~10KB)      │  (3h, ~12KB)
                     │
       UI Polish        │
       (5h, ~30KB)      │
                     │
                     └─────────────────→
                   LOW         HIGH
                   EFFORT      EFFORT

PICKED FOR YOU: Smart Cleanup ✓ (Best ROI)
```

---

## ✨ RECOMMENDED: Feature #1 - Smart Cleanup 🗑️

**Difficulty**: ⭐⭐☆☆☆ (Easy-Medium)  
**Time Estimate**: 1-2 hours  
**Impact**: Medium (saves device storage)  
**Files to Modify**: 3-4  
**Tests to Add**: 2-3  

### What it does
Users can optionally delete files from camera/SD card after successful sync to Immich. Includes:
- Checkbox in settings: "Delete after sync"
- Dry-run preview showing what will be deleted
- Confirmation dialog before deletion
- Safety: Only delete if upload succeeded

### Files to Create/Modify

```
PotopopiCamSync/Models/AppConfig.cs
  ✅ Add: DeleteAfterSync (bool) property

PotopopiCamSync/Services/SyncOrchestrator.cs
  ✅ Add: DeleteAfterSyncAsync() method
  ✅ Call in main sync loop after successful upload

PotopopiCamSync/ViewModels/MainViewModel.cs
  ✅ Add: DeleteAfterSyncEnabled (bool property)
  ✅ Bind to UI checkbox

PotopopiCamSync/Views/SettingsWindow.xaml
  ✅ Add: CheckBox for "Delete after sync"
  ✅ Add: "Preview" button to show what will be deleted

PotopopiCamSync.Tests/SyncOrchestratorTests.cs
  ✅ Test: DeleteAfterSync_Deletes_Synced_Files
  ✅ Test: DeleteAfterSync_Respects_DeleteAfterSync_Setting
  ✅ Test: DeleteAfterSync_Does_Not_Delete_Failed_Uploads
```

### Implementation Outline

```csharp
// 1. In AppConfig.cs
public class AppConfig
{
    public bool DeleteAfterSync { get; set; } = false;
    // ... existing properties
}

// 2. In SyncOrchestrator.cs
private async Task DeleteAfterSyncAsync(SyncFile file, bool success)
{
    if (!success || !_config.DeleteAfterSync) return;

    try
    {
        await _device.DeleteFileAsync(file);
        Progress($"Deleted: {file.FileName}");
    }
    catch (Exception ex)
    {
        Progress($"Warning: Could not delete {file.FileName}: {ex.Message}");
    }
}

// 3. Call after successful upload
if (await _immichSync.UploadAsync(localFile, file))
{
    await DeleteAfterSyncAsync(file, success: true);
}

// 4. In SettingsWindow.xaml.cs
private void DeleteAfterSync_Checked(object sender, RoutedEventArgs e)
{
    _config.DeleteAfterSync = ((CheckBox)sender).IsChecked ?? false;
    _settingsService.SaveConfig(_config);
}

// 5. In Tests
[Fact]
public async Task DeleteAfterSync_Deletes_Synced_Files()
{
    var config = new AppConfig { DeleteAfterSync = true };
    var orchestrator = new SyncOrchestrator(_device, _config, ...);

    var file = new SyncFile { FileName = "test.jpg" };
    await orchestrator.StartSyncAsync();

    // Verify device.DeleteFileAsync was called
    _device.Verify(d => d.DeleteFileAsync(It.IsAny<SyncFile>()), Times.Once);
}
```

### Code Diff Preview

**AppConfig.cs**:
```diff
  public class AppConfig
  {
+     public bool DeleteAfterSync { get; set; } = false;
      public string? ImmichUrl { get; set; }
      // ...
  }
```

**SyncOrchestrator.cs** (in StartSyncAsync):
```diff
  foreach (var file in filesToSync)
  {
      var localPath = Path.Combine(_config.LocalFolder, file.FileName);
      await Download(file, localPath);

      if (await _immichSync.UploadAsync(localPath, file))
      {
+         if (_config.DeleteAfterSync)
+         {
+             await _device.DeleteFileAsync(file);
+         }
      }
  }
```

---

## ✨ ALTERNATIVE: Feature #2 - Exclusion Filters 🔍

**Difficulty**: ⭐⭐⭐☆☆ (Medium)  
**Time Estimate**: 2-3 hours  
**Impact**: Medium (skip unwanted files)  
**Files to Modify**: 4-5  

### What it does
Skip files matching patterns (*.RAW, *.TEMP, THUMBS.DB, etc):
- Glob pattern support: `*.RAW`, `IMG_*.jpg`
- Regex support: `^TEMP_.*$`
- Per-device rules stored in config
- UI to manage filter list

### Implementation

```csharp
// 1. In AppConfig.cs
public class AppConfig
{
    public List<string> ExclusionPatterns { get; set; } = new();
}

// 2. In SyncOrchestrator.cs (GetFilesAsync)
private bool ShouldSkipFile(SyncFile file)
{
    foreach (var pattern in _config.ExclusionPatterns)
    {
        if (FileMatcher.Match(file.FileName, pattern))
            return true;
    }
    return false;
}

// 3. In GetFilesAsync
var filesToSync = allFiles
    .Where(f => !ShouldSkipFile(f))
    .ToList();
```

---

## ✨ ALTERNATIVE: Feature #3 - Enhanced Logging 🔍

**Difficulty**: ⭐⭐☆☆☆ (Easy)  
**Time Estimate**: 1.5-2 hours  
**Impact**: Medium (better debugging)  
**Files to Modify**: 3-4  

### What it does
Replace current FileLogger with Microsoft.Extensions.Logging:
- Structured logging (JSON)
- Log levels: Debug, Info, Warning, Error
- Daily log file rotation
- UI to view recent logs

### Installation
```bash
dotnet add PotopopiCamSync package Microsoft.Extensions.Logging
dotnet add PotopopiCamSync package Microsoft.Extensions.Logging.Console
```

---

## 📋 Implementation Checklist

### For Smart Cleanup Feature

**Phase 1: Code Changes** (45 min)
- [ ] Add `DeleteAfterSync` to AppConfig.cs
- [ ] Implement `DeleteAfterSyncAsync()` in SyncOrchestrator.cs
- [ ] Call delete after successful upload
- [ ] Update SettingsWindow.xaml with checkbox
- [ ] Update MainViewModel with binding

**Phase 2: Testing** (30 min)
- [ ] Write unit test: delete called when enabled
- [ ] Write unit test: delete NOT called when disabled
- [ ] Write unit test: delete NOT called on upload failure
- [ ] Manual test with real device

**Phase 3: Polish** (15 min)
- [ ] Add confirmation dialog
- [ ] Add preview/dry-run functionality
- [ ] Error handling if delete fails
- [ ] User-friendly error messages

**Total**: ~90 min (1.5 hours)

---

## 🎬 How to Get Started Right Now

### Option A: Let Me Implement It (Recommended)

1. Tell me which feature: **"Let's do Smart Cleanup"**
2. I'll implement all code + tests
3. You verify and commit

### Option B: Guide Me Step-by-Step

I'll provide code snippets and you implement in your editor

### Option C: Pair Programming

I guide you through the implementation in real-time

---

## 📊 Feature Comparison Table

| Feature | Difficulty | Time | Impact | Users Want |
|---------|-----------|------|--------|-----------|
| Smart Cleanup | ⭐⭐ | 1-2h | Medium | YES |
| Exclusion Filters | ⭐⭐⭐ | 2-3h | Medium | YES |
| Albums | ⭐⭐⭐ | 2-3h | Low | MAYBE |
| Enhanced Logging | ⭐⭐ | 1.5h | Medium | YES |
| UI Polish | ⭐⭐⭐⭐ | 5h | Low | NICE |
| Auto-Update | ⭐⭐⭐ | 2-3h | Low | MAYBE |

---

## 🎯 My Recommendation

**Start with Smart Cleanup** because:
1. ✅ **Easiest to implement** (basic logic)
2. ✅ **High user value** (saves storage)
3. ✅ **Low risk** (independent feature)
4. ✅ **Quick win** (1-2 hours)
5. ✅ **No new dependencies** (uses existing APIs)

Then follow up with **Exclusion Filters** for:
- ✅ Skip RAW files (photographers love this)
- ✅ Skip temp/cache files
- ✅ Skip system files (THUMBS.DB, etc)

---

## 🚀 Ready to Code?

**I can:**
- ✅ Implement the complete feature with all code
- ✅ Write comprehensive tests
- ✅ Add UI bindings
- ✅ Verify everything compiles and tests pass
- ✅ Commit with proper message
- ✅ Explain every change

**Just tell me**: Which feature should we build next?

---

## 📞 Questions to Consider

1. **Priority**: Should we start with Smart Cleanup or something else?
2. **Style**: Do you want me to implement it fully, or step-by-step?
3. **Scope**: Any other features you want in the same PR?
4. **Testing**: Should I add extra test cases for edge cases?
5. **Documentation**: Should I update README with new features?

---

**Status**: 🟢 Ready to Code 🚀  
**Build**: ✅ Clean  
**Tests**: ✅ 24/24 Passing  
**Next Phase**: Your Choice!
