using System;
using System.Collections.Generic;
using System.Linq;
using DisplayControl.Abstractions;
using DisplayControl.Abstractions.Models;
using DisplayControl.Windows.Helpers;
using DisplayControl.Windows.Interop.User32;
using DisplayControl.Windows.Interop.Shcore;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace DisplayControl.Windows.Services
{
    /// <summary>Windows implementation of IDisplayConfigurator using User32 DisplayConfig and DEVMODE APIs.</summary>
    public class WindowsDisplayConfigurator : IDisplayConfigurator
    {
        /// <summary>Internal model representing a display source (GPU output) in the DisplayConfig topology.</summary>
        private sealed class SourceInfo
        {
            public LUID AdapterId;
            public uint SourceId;
            public string? GdiName;
            public bool Active;
            public (LUID adapter, uint targetId)? ActiveTarget;
            public HashSet<string> CandidateTargets = new();
            public uint Width;
            public uint Height;
            public int PosX;
            public int PosY;
            public bool HasMode;
            public override string ToString() => $"{GdiName} (src:{SourceId}) {(Active ? "ACTIVE" : "-")}";
        }

        /// <summary>Internal model representing a display target (physical monitor) in the DisplayConfig topology.</summary>
        private sealed class TargetInfo
        {
            public LUID AdapterId;
            public uint TargetId;
            public string? Friendly;
            public string? Vendor;
            public string? ProductHex;
            public bool Available;
            public bool Active;
            public (LUID adapter, uint sourceId)? ActiveSource;
            public HashSet<string> CandidateSources = new();
            public double ActiveRefreshHz;
            public bool HasActiveRefresh;
            public DISPLAYCONFIG_ROTATION Rotation;
            public bool HasTransform;
            public override string ToString() => $"'{Friendly}' [{Vendor}/{ProductHex}] (tgt:{TargetId}) {(Active ? "ACTIVE" : "-")}";
        }

        private readonly Dictionary<string, SourceInfo> _sourcesByKey = new();
        private readonly Dictionary<string, TargetInfo> _targetsByKey = new();

        private static string SKey(LUID a, uint sourceId) => $"{a.LowPart}:{a.HighPart}:{sourceId}";
        private static string TKey(LUID a, uint targetId) => $"{a.LowPart}:{a.HighPart}:{targetId}";

        /// <summary>Resolves the GDI device name (e.g., \\\\ .\\\\DISPLAYx) for a given source.</summary>
        private static string? GetSourceGdiName(LUID adapterId, uint sourceId)
        {
            var src = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_SOURCE_NAME,
                    size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                    adapterId = adapterId,
                    id = sourceId
                }
            };
            return User32DisplayConfig.DisplayConfigGetDeviceInfo(ref src) == 0 ? src.viewGdiDeviceName : null;
        }

        /// <summary>Resolves the target friendly name and EDID information for a given target.</summary>
        private static (string? friendly, string? vendor, string? prodHex) GetTargetName(LUID adapterId, uint targetId)
        {
            var tgt = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_TARGET_NAME,
                    size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = adapterId,
                    id = targetId
                }
            };
            if (User32DisplayConfig.DisplayConfigGetDeviceInfo(ref tgt) != 0) return (null, null, null);
            string vendor = EdidHelper.DecodePnP(tgt.edidManufactureId);
            string prod = $"0x{tgt.edidProductCodeId:X4}";
            return (tgt.monitorFriendlyDeviceName, vendor, prod);
        }

        /// <summary>Refreshes internal DisplayConfig source/target indices from the current system configuration.</summary>
        private void FillData()
        {
            _sourcesByKey.Clear();
            _targetsByKey.Clear();

            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                throw new DisplayControlException("Error getting buffer sizes");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                throw new DisplayControlException("Error getting current display configuration");


            var sourceModes = new Dictionary<string, DISPLAYCONFIG_SOURCE_MODE>();
            for (int m = 0; m < modeCount; m++)
            {
                if (modes[m].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE)
                {
                    string key = SKey(modes[m].adapterId, modes[m].id);
                    sourceModes[key] = modes[m].sourceMode;
                }
            }

            for (int i = 0; i < pathCount; i++)
            {
                var p = paths[i];
                if (!p.targetInfo.targetAvailable) continue;

                var (friendly, vendor, prodHex) = GetTargetName(p.targetInfo.adapterId, p.targetInfo.id);
                bool targetAvail = p.targetInfo.targetAvailable;

                string? gdiName = GetSourceGdiName(p.sourceInfo.adapterId, p.sourceInfo.id);

                bool active = (p.flags & DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE) != 0;

                string sKey = SKey(p.sourceInfo.adapterId, p.sourceInfo.id);
                if (!_sourcesByKey.TryGetValue(sKey, out var sInfo))
                {
                    sInfo = new SourceInfo
                    {
                        AdapterId = p.sourceInfo.adapterId,
                        SourceId = p.sourceInfo.id,
                        GdiName = gdiName,
                        Active = active
                    };
                    _sourcesByKey[sKey] = sInfo;
                }
                else sInfo.GdiName ??= gdiName;

                if (sourceModes.TryGetValue(sKey, out var sm))
                {
                    sInfo.Width = sm.width;
                    sInfo.Height = sm.height;
                    sInfo.PosX = sm.position.x;
                    sInfo.PosY = sm.position.y;
                    sInfo.HasMode = true;
                }

                string tKey = TKey(p.targetInfo.adapterId, p.targetInfo.id);
                if (!_targetsByKey.TryGetValue(tKey, out var tInfo))
                {
                    tInfo = new TargetInfo
                    {
                        AdapterId = p.targetInfo.adapterId,
                        TargetId = p.targetInfo.id,
                        Friendly = friendly,
                        Vendor = vendor,
                        ProductHex = prodHex,
                        Available = targetAvail,
                        Active = active
                    };
                    _targetsByKey[tKey] = tInfo;
                }
                else
                {
                    tInfo.Friendly ??= friendly;
                    tInfo.Vendor ??= vendor;
                    tInfo.ProductHex ??= prodHex;
                    tInfo.Available |= targetAvail;
                }

                if (p.targetInfo.refreshRate.Denominator != 0)
                {
                    tInfo.ActiveRefreshHz = (double)p.targetInfo.refreshRate.Numerator / p.targetInfo.refreshRate.Denominator;
                    tInfo.HasActiveRefresh = true;
                }

                // Note: We no longer compute desktop refresh independently; ActiveRefreshHz is sufficient for simplified output.
                tInfo.Rotation = p.targetInfo.rotation;
                tInfo.HasTransform = true;

                if (!tInfo.Active && !active && !sInfo.Active)
                    sInfo.CandidateTargets.Add(tKey);
                if (!sInfo.Active && !active && !tInfo.Active)
                    tInfo.CandidateSources.Add(sKey);

                if (active)
                {
                    sInfo.ActiveTarget = (p.targetInfo.adapterId, p.targetInfo.id);
                    tInfo.ActiveSource = (p.sourceInfo.adapterId, p.sourceInfo.id);
                }
            }
        }

        /// <summary>Enables an available inactive monitor or one matching the given friendly name.</summary>
        public Result EnableMonitor(string? friendlyName = null)
        {
            FillData();
            if (friendlyName != null)
            {
                if (_targetsByKey.Values.Any(t => t.Active &&
                    !string.IsNullOrWhiteSpace(t.Friendly) &&
                    t.Friendly.Contains(friendlyName, StringComparison.OrdinalIgnoreCase)))
                    return Result.Ok("The monitor is already active");
            }

            var allCandidates = _targetsByKey.Values.Where(t => t.Available && !t.Active).ToList();
            if (allCandidates.Count == 0)
            {
                return Result.Fail("No monitors available; check connection and power.");
            }

            var candidates = _targetsByKey.Values
                .Where(t => t.Available && !t.Active &&
                            (friendlyName == null ||
                             (!string.IsNullOrWhiteSpace(t.Friendly) &&
                              t.Friendly.Contains(friendlyName, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            if (candidates.Count == 0)
            {
                var optsList = allCandidates.Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                string opts = string.Join(", ", optsList);
                return Result.Fail($"Could not find the specified monitor. Options: {opts}", optsList);
            }

            if (friendlyName == null && candidates.Count > 1)
            {
                var optsList = candidates.Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                string opts = string.Join(", ", optsList);
                return Result.Fail($"Multiple available monitors. Specify a name. Options: {opts}", optsList);
            }

            TargetInfo target =
                candidates.FirstOrDefault(t => t.CandidateSources.Any(sk => _sourcesByKey.TryGetValue(sk, out var s) && !s.Active))
                ?? candidates.First();

            var freeSourceKey = target.CandidateSources
                .FirstOrDefault(sk => _sourcesByKey.TryGetValue(sk, out var s) && !s.Active);

            if (freeSourceKey == null)
                return Result.Fail("No compatible free source available.");

            var freeSource = _sourcesByKey[freeSourceKey];
            bool ok = SetMonitor((int)freeSource.SourceId, (int)target.TargetId);
            return ok ? Result.Ok() : Result.Fail("SetDisplayConfig failed");
        }

        /// <summary>Activates a path between the specified source and target and applies it.</summary>
        private bool SetMonitor(int sourceid, int targetid)
        {
            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                throw new DisplayControlException("Error getting buffer sizes");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                throw new DisplayControlException("Error getting current display configuration");

            for (int i = 0; i < pathCount; i++)
            {
                if (paths[i].sourceInfo.id == sourceid && paths[i].targetInfo.id == targetid)
                    paths[i].flags |= DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE;
            }

            var activePaths = paths.Where(p => (p.flags & DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE) != 0).ToArray();

            var flags = SDC.USE_SUPPLIED_DISPLAY_CONFIG
                      | SDC.APPLY
                      | SDC.SAVE_TO_DATABASE
                      | SDC.ALLOW_CHANGES;

            int rc = User32DisplayConfig.SetDisplayConfig((uint)activePaths.Length, activePaths, (uint)modes.Length, modes, flags);
            bool ok = rc == 0;

            if (ok)
            {
                FillData();
            }

            return ok;
        }

        /// <summary>Disables an active monitor resolved by GDI device name or friendly name.</summary>
        public Result DisableMonitor(string displayOrName)
        {
            FillData();


            (LUID adapter, uint sourceId, uint targetId)? match = null;

            bool isGdi = !string.IsNullOrWhiteSpace(displayOrName) && displayOrName.StartsWith("\\\\.\\DISPLAY", System.StringComparison.OrdinalIgnoreCase);
            if (isGdi)
            {

                var srcAny = _sourcesByKey.Values.FirstOrDefault(s => s.GdiName != null && s.GdiName.Equals(displayOrName, StringComparison.OrdinalIgnoreCase));
                if (srcAny != null && (!srcAny.Active || !srcAny.ActiveTarget.HasValue))
                    return Result.Ok("The monitor was already disabled");


                var src = _sourcesByKey.Values.FirstOrDefault(s => s.GdiName != null && s.GdiName.Equals(displayOrName, StringComparison.OrdinalIgnoreCase) && s.ActiveTarget.HasValue);
                if (src != null && src.ActiveTarget is var at && at.HasValue)
                    match = (src.AdapterId, src.SourceId, at.Value.targetId);
            }
            else
            {

                var matchingTargets = _targetsByKey.Values
                    .Where(t => !string.IsNullOrWhiteSpace(t.Friendly) && t.Friendly!.Contains(displayOrName, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var tgtActive = matchingTargets.FirstOrDefault(t => t.Active);
                if (tgtActive == null)
                {
                    if (matchingTargets.Count > 0)
                        return Result.Ok("The monitor was already disabled");
                }
                else if (tgtActive.ActiveSource is var asrc && asrc.HasValue)
                {
                    match = (tgtActive.AdapterId, asrc.Value.sourceId, tgtActive.TargetId);
                }
            }

            if (match == null)
            {

                var activeOpts = _targetsByKey.Values
                    .Where(t => t.Active)
                    .Select(t => t.Friendly ?? $"tgt:{t.TargetId}")
                    .ToList();
                string opts = activeOpts.Count > 0 ? $" Options: {string.Join(", ", activeOpts)}" : string.Empty;
                return Result.Fail("No matching active monitor found." + opts, activeOpts);
            }


            int activos = _targetsByKey.Values.Count(t => t.Active);
            if (activos <= 1)
                return Result.Fail("Cannot disable the only active monitor");


            string sKey = SKey(match.Value.adapter, match.Value.sourceId);
            if (_sourcesByKey.TryGetValue(sKey, out var srcInfo))
            {
                bool esPrimario = srcInfo.HasMode && srcInfo.PosX == 0 && srcInfo.PosY == 0;
                if (esPrimario)
                {

                    int primariosEnCero = _targetsByKey.Values.Count(t =>
                        t.Active && t.ActiveSource.HasValue &&
                        _sourcesByKey.TryGetValue(SKey(t.ActiveSource.Value.adapter, t.ActiveSource.Value.sourceId), out var ts) &&
                        ts.HasMode && ts.PosX == 0 && ts.PosY == 0);
                    if (primariosEnCero == 1)
                    {
                        var activeOpts = _targetsByKey.Values
                            .Where(t => t.Active)
                            .Select(t => t.Friendly ?? $"tgt:{t.TargetId}")
                            .ToList();
                        string opts = activeOpts.Count > 0 ? $" Options: {string.Join(", ", activeOpts)}" : string.Empty;
                        return Result.Fail(
                            "Cannot disable the primary monitor. Change the primary first with 'displayctl setprimary <option>'." + opts,
                            activeOpts,
                            "Use 'displayctl setprimary <friendly>' to choose the primary");
                    }
                }
            }


            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                return Result.Fail("Error getting buffer sizes");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                return Result.Fail("Error getting current display configuration");

            for (int i = 0; i < pathCount; i++)
            {
                bool isTarget = paths[i].targetInfo.id == match.Value.targetId &&
                                paths[i].sourceInfo.id == match.Value.sourceId &&
                                paths[i].targetInfo.adapterId.LowPart == match.Value.adapter.LowPart &&
                                paths[i].targetInfo.adapterId.HighPart == match.Value.adapter.HighPart;
                if (isTarget)
                {

                    paths[i].flags &= ~DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE;
                }
            }

            var activePaths = paths.Where(p => (p.flags & DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE) != 0).ToArray();

            var flags = SDC.USE_SUPPLIED_DISPLAY_CONFIG | SDC.APPLY | SDC.SAVE_TO_DATABASE | SDC.ALLOW_CHANGES;

            int rc = User32DisplayConfig.SetDisplayConfig((uint)activePaths.Length, activePaths, (uint)modes.Length, modes, flags);
            bool ok = rc == 0;
            if (ok) FillData();
            return ok ? Result.Ok() : Result.Fail("SetDisplayConfig failed to disable");
        }

        /// <summary>Sets the given display as primary by GDI device name or friendly name.</summary>
        public Result SetPrimary(string displayOrName)
        {
            FillData();


            SourceInfo? targetSource = null;
            TargetInfo? targetTarget = null;

            bool isGdi = !string.IsNullOrWhiteSpace(displayOrName) && displayOrName.StartsWith("\\\\.\\DISPLAY", StringComparison.OrdinalIgnoreCase);
            if (isGdi)
            {
                targetSource = _sourcesByKey.Values.FirstOrDefault(s => s.GdiName != null && s.GdiName.Equals(displayOrName, StringComparison.OrdinalIgnoreCase));
                if (targetSource == null)
                {
                    var activeOpts = _targetsByKey.Values.Where(t => t.Active).Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                    string opts = activeOpts.Count > 0 ? $" Options: {string.Join(", ", activeOpts)}" : string.Empty;
                    return Result.Fail("Display not found." + opts, activeOpts, "Use 'displayctl setprimary <friendly>'");
                }
                if (!targetSource.Active || !targetSource.ActiveTarget.HasValue)
                {
                    var inactiveOpts = _targetsByKey.Values.Where(t => !t.Active).Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                    string opts = inactiveOpts.Count > 0 ? $" You can enable it with 'displayctl enable <friendly>'. Options: {string.Join(", ", inactiveOpts)}" : string.Empty;
                    return Result.Fail("Monitor must be active to become primary." + opts, inactiveOpts, "Use 'displayctl enable <friendly>'");
                }
                string tKey = TKey(targetSource.ActiveTarget.Value.adapter, targetSource.ActiveTarget.Value.targetId);
                _targetsByKey.TryGetValue(tKey, out targetTarget);
            }
            else
            {
                targetTarget = _targetsByKey.Values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Friendly) && t.Friendly!.Contains(displayOrName, StringComparison.OrdinalIgnoreCase));
                if (targetTarget == null)
                {
                    var activeOpts = _targetsByKey.Values.Where(t => t.Active).Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                    string opts = activeOpts.Count > 0 ? $" Options: {string.Join(", ", activeOpts)}" : string.Empty;
                    return Result.Fail("Monitor not found." + opts, activeOpts, "Use 'displayctl setprimary <friendly>'");
                }
                if (!targetTarget.Active || !targetTarget.ActiveSource.HasValue)
                {
                    var inactiveOpts = _targetsByKey.Values.Where(t => !t.Active).Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                    string opts = inactiveOpts.Count > 0 ? $" You can enable it with 'displayctl enable <friendly>'. Options: {string.Join(", ", inactiveOpts)}" : string.Empty;
                    return Result.Fail("Monitor must be active to become primary." + opts, inactiveOpts, "Use 'displayctl enable <friendly>'");
                }
                string sKey = SKey(targetTarget.ActiveSource.Value.adapter, targetTarget.ActiveSource.Value.sourceId);
                _sourcesByKey.TryGetValue(sKey, out targetSource);
            }

            if (targetSource == null || targetTarget == null)
                return Result.Fail("Could not resolve target monitor");


            if (targetSource.HasMode && targetSource.PosX == 0 && targetSource.PosY == 0)
                return Result.Ok("The monitor was already primary");


            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                return Result.Fail("Error getting buffer sizes");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                return Result.Fail("Error getting current display configuration");


            int targetModeIndex = -1;
            var targetAdapter = targetSource.AdapterId;
            uint targetSourceId = targetSource.SourceId;

            int prevX = 0;
            int prevY = 0;

            for (int i = 0; i < modeCount; i++)
            {
                if (modes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE)
                {
                    if (modes[i].adapterId.LowPart == targetAdapter.LowPart && modes[i].adapterId.HighPart == targetAdapter.HighPart && modes[i].id == targetSourceId)
                    {
                        targetModeIndex = i;
                        prevX = modes[i].sourceMode.position.x;
                        prevY = modes[i].sourceMode.position.y;
                    }
                }
            }

            if (targetModeIndex < 0)
                return Result.Fail("Could not find the target monitor mode");


            int dx = -prevX;
            int dy = -prevY;

            for (int i = 0; i < modeCount; i++)
            {
                if (modes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.SOURCE)
                {
                    modes[i].sourceMode.position.x += dx;
                    modes[i].sourceMode.position.y += dy;
                }
            }

            var activePaths = paths.Where(p => (p.flags & DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE) != 0).ToArray();
            var flags = SDC.USE_SUPPLIED_DISPLAY_CONFIG | SDC.APPLY | SDC.SAVE_TO_DATABASE | SDC.ALLOW_CHANGES;
            int rc = User32DisplayConfig.SetDisplayConfig((uint)activePaths.Length, activePaths, (uint)modes.Length, modes, flags);
            bool ok = rc == 0;
            if (ok) FillData();
            return ok ? Result.Ok() : Result.Fail("SetDisplayConfig failed to set primary");
        }

        /// <summary>Enables/disables monitors according to the desired states.</summary>
        public Result SetMonitors(IEnumerable<DesiredMonitor> desiredStates)
        {
            if (desiredStates == null)
                return Result.Fail("Parameter 'desiredStates' is null");

            var desiredEnabled = new HashSet<string>(
                desiredStates
                    .Where(d => d.Enabled && !string.IsNullOrWhiteSpace(d.Name))
                    .Select(d => d.Name),
                StringComparer.OrdinalIgnoreCase);

            var current = List();
            var available = new HashSet<string>(
                current.Select(d => d.FriendlyName).Where(n => !string.IsNullOrWhiteSpace(n))!.Cast<string>(),
                StringComparer.OrdinalIgnoreCase);

            var missing = desiredEnabled.Where(n => !available.Contains(n)).ToList();
            if (missing.Count > 0)
            {
                var opts = available.ToList();
                return Result.Fail(
                    $"Requested monitors not found: {string.Join(", ", missing)}.",
                    opts,
                    "Verifique el nombre y que el monitor estÃƒÂ© conectado");
            }

            foreach (var name in desiredEnabled)
            {
                var r = EnableMonitor(name);
                if (!r.Success) return r;
            }

            var afterEnable = List();

            if (desiredEnabled.Count > 0)
            {
                var currentPrimary = afterEnable.FirstOrDefault(d => d.IsActive && d.IsPrimary);
                bool needPrimaryChange = currentPrimary == null
                                         || string.IsNullOrWhiteSpace(currentPrimary.FriendlyName)
                                         || !desiredEnabled.Contains(currentPrimary.FriendlyName);
                if (needPrimaryChange)
                {
                    var targetPrimary = afterEnable.FirstOrDefault(d => d.IsActive &&
                                                                        !string.IsNullOrWhiteSpace(d.FriendlyName) &&
                                                                        desiredEnabled.Contains(d.FriendlyName!));
                    if (targetPrimary != null && !string.IsNullOrWhiteSpace(targetPrimary.FriendlyName))
                    {
                        var rp = SetPrimary(targetPrimary.FriendlyName!);
                        if (!rp.Success) return rp;
                    }
                }
            }

            var afterPrimary = List();
            var toDisable = afterPrimary.Where(d => d.IsActive && (string.IsNullOrWhiteSpace(d.FriendlyName) || !desiredEnabled.Contains(d.FriendlyName!)))
                                        .ToList();
            foreach (var d in toDisable)
            {
                var key = d.FriendlyName ?? d.Active?.GdiName;
                if (string.IsNullOrWhiteSpace(key)) continue;
                var rd = DisableMonitor(key!);
                if (!rd.Success) return rd;
            }

            return Result.Ok("Profile applied");
        }

        /// <summary>Applies a full display profile including primary, geometry and refresh with rollback + confirmation.</summary>
        public Result SetMonitors(DesiredProfile profile)
        {
            if (profile == null || profile.Monitors == null || profile.Monitors.Count == 0)
                return Result.Fail("Empty or null profile");

            // Capture current layout to enable rollback on failure or if user does not confirm.
            var snapshot = CreateSnapshotProfile();

            // Apply the requested profile.
            var apply = ApplyFullProfile(profile);
            if (!apply.Success)
            {
                var rb = ApplyFullProfile(snapshot);
                if (rb.Success)
                {
                    return Result.Fail("Failed to apply profile; previous layout restored");
                }
                else
                {
                    FallbackEnsurePrimary(snapshot.PrimaryName);
                    return Result.Fail("Failed to apply profile; applied safety fallback (could not restore full layout)");
                }
            }

            // Ask for confirmation with a top-most dialog; revert on timeout or cancel.
            bool keep = PromptKeepChangesWithTimeout(
                title: "DisplayControl — Keep display changes?",
                message: "Keep these display settings?\nChanges will revert automatically in 15 seconds.",
                timeoutSeconds: 15);

            if (keep)
                return Result.Ok($"Profile '{profile.Name ?? "(unnamed)"}' applied and confirmed");

            var revert = ApplyFullProfile(snapshot);
            if (!revert.Success)
            {
                FallbackEnsurePrimary(snapshot.PrimaryName);
                return Result.Fail("Changes reverted using safety fallback (could not restore full layout)");
            }

            return Result.Ok("Changes reverted");
        }

        /// <summary>
        /// Builds an in-memory DesiredProfile snapshot of the current layout.
        /// </summary>
        private DesiredProfile CreateSnapshotProfile()
        {
            var current = List();
            string? primary = current.FirstOrDefault(c => c.IsActive && c.IsPrimary)?.FriendlyName;
            var monitors = new List<DesiredMonitorConfig>(current.Count);
            foreach (var d in current)
            {
                if (string.IsNullOrWhiteSpace(d.FriendlyName)) continue;
                var a = d.Active;
                monitors.Add(new DesiredMonitorConfig(
                    d.FriendlyName!,
                    d.IsActive,
                    a?.PositionX ?? 0,
                    a?.PositionY ?? 0,
                    a?.Width ?? 0,
                    a?.Height ?? 0,
                    a?.RefreshHz ?? 0.0,
                    a?.Orientation,
                    null
                ));
            }
            return new DesiredProfile("snapshot", primary, monitors);
        }

        /// <summary>
        /// Applies a full profile (enable/disable, set primary, geometry, refresh). Does not prompt.
        /// </summary>
        private Result ApplyFullProfile(DesiredProfile profile)
        {
            var current = List();
            var available = new HashSet<string>(current.Select(c => c.FriendlyName).Where(n => !string.IsNullOrWhiteSpace(n))!.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            var missing = profile.Monitors.Select(m => m.Name).Where(n => !available.Contains(n)).ToList();
            if (missing.Count > 0)
                return Result.Fail($"Monitors not found in the system: {string.Join(", ", missing)}", available.ToList());

            if (!string.IsNullOrWhiteSpace(profile.PrimaryName))
            {
                var r1 = EnableMonitor(profile.PrimaryName);
                if (!r1.Success) return r1;
                var r2 = SetPrimary(profile.PrimaryName);
                if (!r2.Success) return r2;
            }

            var plan = profile.Monitors.Select(m => new DesiredMonitor(m.Name, m.Enabled)).ToList();
            var r = SetMonitors((IEnumerable<DesiredMonitor>)plan);
            if (!r.Success) return r;

            var r3 = ApplyDisplaySettings(profile);
            if (!r3.Success) return r3;

            return Result.Ok();
        }

        /// <summary>
        /// Best-effort fallback: enable and set as primary the provided monitor name.
        /// </summary>
        private void FallbackEnsurePrimary(string? primaryName)
        {
            if (string.IsNullOrWhiteSpace(primaryName)) return;
            try { _ = EnableMonitor(primaryName!); } catch { }
            try { _ = SetPrimary(primaryName!); } catch { }
        }

        /// <summary>
        /// Shows a top-most confirmation dialog and waits up to the specified timeout.
        /// Returns true to keep changes; false to revert (cancel or timeout).
        /// </summary>
        private bool PromptKeepChangesWithTimeout(string title, string message, int timeoutSeconds)
        {
            using var done = new ManualResetEvent(false);
            bool? keep = null;
            string caption = title;

            Thread t = new Thread(() =>
            {
                try
                {
                    int res = Interop.User32.User32Dialogs.MessageBoxTopMost(IntPtr.Zero, message, caption,
                        Interop.User32.User32Dialogs.MB_YESNO | Interop.User32.User32Dialogs.MB_ICONQUESTION | Interop.User32.User32Dialogs.MB_SETFOREGROUND);
                    keep = (res == Interop.User32.User32Dialogs.IDYES);
                }
                catch
                {
                    keep = false;
                }
                finally
                {
                    done.Set();
                }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            if (!done.WaitOne(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))))
            {
                // Timeout: attempt to close the message box window and revert.
                try
                {
                    var hWnd = Interop.User32.User32Dialogs.FindWindowByCaption(caption);
                    if (hWnd != IntPtr.Zero)
                    {
                        Interop.User32.User32Dialogs.PostClose(hWnd);
                    }
                }
                catch { }
                return false;
            }

            return keep ?? false;
        }

        private static uint MapOrientationToDm(string? orientation)
            => (orientation ?? string.Empty).ToLowerInvariant() switch
            {
                "rotate90" => 1u,
                "rotate180" => 2u,
                "rotate270" => 3u,
                _ => 0u
            };

        /// <summary>Applies DEVMODE-based per-monitor settings and performs a global apply.</summary>
        private Result ApplyDisplaySettings(DesiredProfile profile)
        {
            foreach (var m in profile.Monitors.Where(m => m.Enabled))
            {
                var gdi = ResolveGdiByFriendly(m.Name);
                if (string.IsNullOrWhiteSpace(gdi))
                    return Result.Fail($"Could not resolve the GDI device for '{m.Name}'");

                var dm = new DEVMODE
                {
                    dmSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<DEVMODE>()
                };

                uint fields = 0;
                bool isPrimary = !string.IsNullOrWhiteSpace(profile.PrimaryName) &&
                                 string.Equals(profile.PrimaryName, m.Name, StringComparison.OrdinalIgnoreCase);
                if (!isPrimary)
                {
                    dm.dmPositionX = m.PositionX;
                    dm.dmPositionY = m.PositionY;
                    fields |= User32DisplaySettings.DM_POSITION;
                }

                if (m.Width > 0 && m.Height > 0)
                {
                    dm.dmPelsWidth = m.Width;
                    dm.dmPelsHeight = m.Height;
                    fields |= User32DisplaySettings.DM_PELSWIDTH | User32DisplaySettings.DM_PELSHEIGHT;
                }

                if (m.DesiredRefreshHz > 0)
                {
                    dm.dmDisplayFrequency = (uint)Math.Round(m.DesiredRefreshHz);
                    fields |= User32DisplaySettings.DM_DISPLAYFREQUENCY;
                }

                if (!string.IsNullOrWhiteSpace(m.Orientation))
                {
                    dm.dmDisplayOrientation = MapOrientationToDm(m.Orientation);
                    fields |= User32DisplaySettings.DM_DISPLAYORIENTATION;
                }

                dm.dmFields = fields;

                if (fields != 0)
                {
                    int rc = User32DisplaySettings.ChangeDisplaySettingsEx(gdi!, ref dm, IntPtr.Zero,
                        User32DisplaySettings.CDS_UPDATEREGISTRY | User32DisplaySettings.CDS_NORESET, IntPtr.Zero);
                    if (rc != User32DisplaySettings.DISP_CHANGE_SUCCESSFUL)
                        return Result.Fail($"Failed to prepare settings for '{m.Name}' (rc={rc})");
                }
            }
            int rcApply = User32DisplaySettings.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            if (rcApply != User32DisplaySettings.DISP_CHANGE_SUCCESSFUL)
                return Result.Fail($"Failed to apply settings (rc={rcApply})");
            FillData();
            return Result.Ok();
        }

        /// <summary>Resolves the GDI device name (\\\\.\\\\DISPLAYx) for the given friendly name, if currently active.</summary>
        private string? ResolveGdiByFriendly(string friendly)
        {
            FillData();
            var tgt = _targetsByKey.Values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Friendly) &&
                                                               t.Friendly!.Equals(friendly, StringComparison.OrdinalIgnoreCase) &&
                                                               t.ActiveSource.HasValue);
            if (tgt == null) return null;
            string sKey = SKey(tgt.ActiveSource!.Value.adapter, tgt.ActiveSource!.Value.sourceId);
            if (_sourcesByKey.TryGetValue(sKey, out var s) && !string.IsNullOrWhiteSpace(s.GdiName))
                return s.GdiName;
            return null;
        }

        /// <summary>Saves the current layout as a profile JSON file under the user's .ewDisplayControl directory.</summary>
        public Result SaveProfile(string? name = null)
        {
            var current = List();
            if (current.Count == 0) return Result.Fail("Could not retrieve monitors");

            string? primary = current.FirstOrDefault(c => c.IsActive && c.IsPrimary)?.FriendlyName;
            var monitors = new List<DesiredMonitorConfig>(current.Count);
            foreach (var d in current)
            {
                if (string.IsNullOrWhiteSpace(d.FriendlyName)) continue;
                var a = d.Active;
                monitors.Add(new DesiredMonitorConfig(
                    d.FriendlyName!,
                    d.IsActive,
                    a?.PositionX ?? 0,
                    a?.PositionY ?? 0,
                    a?.Width ?? 0,
                    a?.Height ?? 0,
                    a?.RefreshHz ?? 0.0,
                    a?.Orientation,
                    null
                ));
            }

            var profile = new DesiredProfile(name ?? "current", primary, monitors);

            try
            {
                string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dir = Path.Combine(userDir, ".ewDisplayControl");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, (name ?? "current") + ".json");
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return Result.Ok($"Profile saved to {path}");
            }
            catch (Exception ex)
            {
                return Result.Fail($"Could not save profile: {ex.Message}");
            }
        }

        /// <summary>Lists monitors available on the system with their current state and details.</summary>
        public IReadOnlyList<DisplayInfo> List()
        {
            FillData();
            var result = new List<DisplayInfo>(_targetsByKey.Count);
            foreach (var t in _targetsByKey.Values.Where(t => t.Available))
            {
                if (t.Active && t.ActiveSource is var asrc && asrc.HasValue)
                {
                    string sk = SKey(t.AdapterId, asrc.Value.sourceId);
                    if (_sourcesByKey.TryGetValue(sk, out var s))
                    {
                        bool isPrimary = s.HasMode && s.PosX == 0 && s.PosY == 0;
                        int? devmodeHz = null;
                        string? orientationStr = t.HasTransform ? t.Rotation.ToString() : null;
                        if (!string.IsNullOrWhiteSpace(s.GdiName))
                        {
                            try
                            {
                                var dm = new DEVMODE();
                                dm.dmSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<DEVMODE>();
                                if (User32DisplaySettings.EnumDisplaySettingsEx(s.GdiName!, User32DisplaySettings.ENUM_CURRENT_SETTINGS, ref dm, User32DisplaySettings.EDS_ROTATEDMODE))
                                {
                                    if (dm.dmDisplayFrequency > 0) devmodeHz = (int)dm.dmDisplayFrequency;
                                    orientationStr = dm.dmDisplayOrientation switch
                                    {
                                        1 => "Rotate90",
                                        2 => "Rotate180",
                                        3 => "Rotate270",
                                        _ => "Identity"
                                    };
                                }
                            }
                            catch { }
                        }

                        double activeHzOut = t.HasActiveRefresh ? t.ActiveRefreshHz : 0.0;
                        if (devmodeHz.HasValue && devmodeHz.Value > 0)
                            activeHzOut = devmodeHz.Value;

                        var active = new ActiveDetails(
                            s.GdiName,
                            s.PosX,
                            s.PosY,
                            s.Width,
                            s.Height,
                            activeHzOut,
                            orientationStr
                        );
                        result.Add(new DisplayInfo(t.Friendly, true, isPrimary, active, t.TargetId));
                        continue;
                    }
                }
                result.Add(new DisplayInfo(t.Friendly, false, false, null, t.TargetId));
            }
            return result;
        }
    }
}

