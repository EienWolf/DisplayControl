using System;
using System.Collections.Generic;
using System.Linq;
using DisplayControl.Abstractions;
using DisplayControl.Abstractions.Models;
using DisplayControl.Windows.Helpers;
using DisplayControl.Windows.Interop.User32;
using DisplayControl.Windows.Interop.Shcore;

namespace DisplayControl.Windows.Services
{
    public class WindowsDisplayConfigurator : IDisplayConfigurator
    {
        private class SourceInfo
        {
            public LUID AdapterId;
            public uint SourceId;
            public string? GdiName;                   // \\ .\DISPLAYx
            public bool Active;
            public (LUID adapter, uint targetId)? ActiveTarget; // si está activo, a qué target está asociado
            public HashSet<string> CandidateTargets = new();    // keys de Target que puede alimentar
            // Datos de modo
            public uint Width;
            public uint Height;
            public int PosX;
            public int PosY;
            public bool HasMode;
            public override string ToString() => $"{GdiName} (src:{SourceId}) {(Active ? "ACTIVE" : "-")}";
        }

        private class TargetInfo
        {
            public LUID AdapterId;
            public uint TargetId;
            public string? Friendly;
            public string? Vendor;
            public string? ProductHex;
            public bool Available;
            public bool Active;
            public (LUID adapter, uint sourceId)? ActiveSource; // si está activo, a qué source está asociado
            public HashSet<string> CandidateSources = new();    // keys de Source que lo pueden alimentar
            // Frecuencia
            public double ActiveRefreshHz;
            public double DesktopRefreshHz;
            public bool HasActiveRefresh;
            // Orientación y escalado
            public DISPLAYCONFIG_ROTATION Rotation;
            public DISPLAYCONFIG_SCALING Scaling;
            public bool HasTransform;
            // Color avanzado / HDR
            public bool? HdrSupported;
            public bool? HdrEnabled;
            public string? ColorEncoding;
            public int? BitsPerColor;
            public override string ToString() => $"'{Friendly}' [{Vendor}/{ProductHex}] (tgt:{TargetId}) {(Active ? "ACTIVE" : "-")}";
        }

        // índices
        private readonly Dictionary<string, SourceInfo> _sourcesByKey = new();
        private readonly Dictionary<string, TargetInfo> _targetsByKey = new();

        private static string SKey(LUID a, uint sourceId) => $"{a.LowPart}:{a.HighPart}:{sourceId}";
        private static string TKey(LUID a, uint targetId) => $"{a.LowPart}:{a.HighPart}:{targetId}";
        private static bool SameAdapter(LUID a, LUID b) => a.LowPart == b.LowPart && a.HighPart == b.HighPart;

        private static Dictionary<string, int> GetTextScaleByGdiName()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                User32Monitor.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr data) =>
                {
                    var mi = new MONITORINFOEX { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
                    if (User32Monitor.GetMonitorInfo(hMon, ref mi))
                    {
                        try
                        {
                            uint dx, dy;
                            int hr = DisplayControl.Windows.Interop.Shcore.ShcoreMethods.GetDpiForMonitor(hMon, DisplayControl.Windows.Interop.Shcore.MonitorDpiType.MDT_EFFECTIVE_DPI, out dx, out dy);
                            if (hr == 0 && dx != 0)
                            {
                                int percent = (int)Math.Round(dx / 96.0 * 100.0);
                                // mi.szDevice es del tipo \\.\\DISPLAYx
                                result[mi.szDevice] = percent;
                                
                            }
                        }
                        catch { /* ignorar si SHCore no disponible */ }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { /* ambientes sin API */ }
            return result;
        }

        // util para leer nombres
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
            string vendor = EdidHelper.DecodePnP((ushort)tgt.edidManufactureId);
            string prod = $"0x{tgt.edidProductCodeId:X4}";
            return (tgt.monitorFriendlyDeviceName, vendor, prod);
        }

        private void FillData()
        {
            _sourcesByKey.Clear();
            _targetsByKey.Clear();

            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                throw new Exception("Error obteniendo tamaños de buffers");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                throw new Exception("Error obteniendo configuración actual");

            // Pre-indexar modos de fuente por adapterId+id
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
                if (p.targetInfo.targetAvailable == false) continue;

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

                // Frecuencia activa desde path.targetInfo.refreshRate
                if (p.targetInfo.refreshRate.Denominator != 0)
                {
                    tInfo.ActiveRefreshHz = (double)p.targetInfo.refreshRate.Numerator / p.targetInfo.refreshRate.Denominator;
                    tInfo.HasActiveRefresh = true;
                }

                // Frecuencia de modo (desktop) si está disponible vía modeInfoIdx
                if (p.targetInfo.modeInfoIdx != 0xFFFFFFFF)
                {
                    var mi = modes[p.targetInfo.modeInfoIdx];
                    if (mi.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.TARGET && mi.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator != 0)
                    {
                        tInfo.DesktopRefreshHz = (double)mi.targetMode.targetVideoSignalInfo.vSyncFreq.Numerator / mi.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator;
                    }
                }

                // Transformaciones
                tInfo.Rotation = p.targetInfo.rotation;
                tInfo.Scaling = p.targetInfo.scaling;
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

            // Consultar HDR y color avanzado por cada target disponible
            foreach (var t in _targetsByKey.Values)
            {
                var adv = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_TARGET_ADVANCED_COLOR_INFO,
                        size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                        adapterId = t.AdapterId,
                        id = t.TargetId
                    }
                };
                int rc = User32DisplayConfig.DisplayConfigGetDeviceInfo(ref adv);
                if (rc == 0)
                {
                    t.HdrSupported = adv.advancedColorSupported;
                    t.HdrEnabled = adv.advancedColorEnabled;
                    t.ColorEncoding = adv.colorEncoding.ToString();
                    t.BitsPerColor = (int)adv.bitsPerColorChannel;
                }
            }

            // Fallback/heurística: si no pudimos determinar HDR o bits por color, usar DEVMODE del source activo
            foreach (var t in _targetsByKey.Values.Where(x => x.Active && x.ActiveSource.HasValue))
            {
                string sKey = SKey(t.ActiveSource!.Value.adapter, t.ActiveSource!.Value.sourceId);
                if (_sourcesByKey.TryGetValue(sKey, out var s) && !string.IsNullOrWhiteSpace(s.GdiName))
                {
                    try
                    {
                        var dm = new DEVMODE();
                        dm.dmSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<DEVMODE>();
                        if (User32DisplaySettings.EnumDisplaySettingsEx(s.GdiName!, User32DisplaySettings.ENUM_CURRENT_SETTINGS, ref dm, 0))
                        {
                            if (!t.BitsPerColor.HasValue && dm.dmBitsPerPel > 0)
                            {
                                int bpp = (int)dm.dmBitsPerPel;
                                t.BitsPerColor = bpp == 30 ? 10 : bpp == 36 ? 12 : bpp >= 24 ? 8 : (int?)null;
                            }
                            // Si HDR no fue detectado por la API, asumir habilitado si hay >=10 bpc
                            if (!t.HdrEnabled.HasValue || t.HdrEnabled == false)
                            {
                                if (t.BitsPerColor.HasValue && t.BitsPerColor.Value >= 10)
                                {
                                    t.HdrEnabled = true;
                                    t.HdrSupported ??= true;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public Result EnableMonitor(string? friendlyName = null)
        {
            FillData();
            if (friendlyName != null)
            {
                if (_targetsByKey.Values.Any(t => t.Active &&
                    !string.IsNullOrWhiteSpace(t.Friendly) &&
                    t.Friendly.Contains(friendlyName, StringComparison.OrdinalIgnoreCase)))
                    return Result.Ok("El monitor ya está activo");
            }

            var allCandidates = _targetsByKey.Values
                .Where(t => t.Available && !t.Active).ToList();

            var candidates = _targetsByKey.Values
                .Where(t => t.Available && !t.Active &&
                            (friendlyName == null ||
                             (!string.IsNullOrWhiteSpace(t.Friendly) &&
                              t.Friendly.Contains(friendlyName, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            if (candidates.Count == 0)
            {
                if (allCandidates.Count == 0)
                {
                    return Result.Fail("No hay monitores disponibles; verifique conexión y encendido.");
                }
                var optsList = allCandidates.Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                string opts = string.Join(", ", optsList);
                return Result.Fail($"No se encontró el monitor especificado. Opciones: {opts}", optsList);
            }

            if (friendlyName == null && candidates.Count > 1)
            {
                var optsList = candidates.Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                string opts = string.Join(", ", optsList);
                return Result.Fail($"Hay varios monitores disponibles. Especifique el nombre. Opciones: {opts}", optsList);
            }

            TargetInfo target =
                candidates.FirstOrDefault(t => t.CandidateSources.Any(sk => _sourcesByKey.TryGetValue(sk, out var s) && !s.Active))
                ?? candidates.First();

            var freeSourceKey = target.CandidateSources
                .FirstOrDefault(sk => _sourcesByKey.TryGetValue(sk, out var s) && !s.Active);

            if (freeSourceKey == null)
                return Result.Fail("No hay source libre compatible.");

            var freeSource = _sourcesByKey[freeSourceKey];
            bool ok = SetMonitor((int)freeSource.SourceId, (int)target.TargetId);
            return ok ? Result.Ok() : Result.Fail("SetDisplayConfig falló");
        }

        private bool SetMonitor(int sourceid, int targetid)
        {
            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                throw new Exception("Error obteniendo tamaños de buffers");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                throw new Exception("Error obteniendo configuración actual");

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

        public Result DisableMonitor(string displayOrName)
        {
            FillData();

            // Resolver por GDI (\\.\DISPLAYx) o por nombre amigable
            (LUID adapter, uint sourceId, uint targetId)? match = null;

            bool isGdi = !string.IsNullOrWhiteSpace(displayOrName) && displayOrName.StartsWith("\\\\.\\DISPLAY", System.StringComparison.OrdinalIgnoreCase);
            if (isGdi)
            {
                // Buscar el source, independientemente de su estado, para saber si ya está deshabilitado
                var srcAny = _sourcesByKey.Values.FirstOrDefault(s => s.GdiName != null && s.GdiName.Equals(displayOrName, StringComparison.OrdinalIgnoreCase));
                if (srcAny != null && (!srcAny.Active || !srcAny.ActiveTarget.HasValue))
                    return Result.Ok("El monitor ya estaba deshabilitado");

                // Si está activo, preparar datos para deshabilitar
                var src = _sourcesByKey.Values.FirstOrDefault(s => s.GdiName != null && s.GdiName.Equals(displayOrName, StringComparison.OrdinalIgnoreCase) && s.ActiveTarget.HasValue);
                if (src != null && src.ActiveTarget is var at && at.HasValue)
                    match = (src.AdapterId, src.SourceId, at.Value.targetId);
            }
            else
            {
                // Si existe algún target que coincida y no está activo, no hay nada que hacer
                var matchingTargets = _targetsByKey.Values
                    .Where(t => !string.IsNullOrWhiteSpace(t.Friendly) && t.Friendly!.Contains(displayOrName, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var tgtActive = matchingTargets.FirstOrDefault(t => t.Active);
                if (tgtActive == null)
                {
                    if (matchingTargets.Count > 0)
                        return Result.Ok("El monitor ya estaba deshabilitado");
                }
                else if (tgtActive.ActiveSource is var asrc && asrc.HasValue)
                {
                    match = (tgtActive.AdapterId, asrc.Value.sourceId, tgtActive.TargetId);
                }
            }

            if (match == null)
            {
                // Ofrecer opciones: monitores activos (por friendly), ya que sólo se pueden deshabilitar activos
                var activeOpts = _targetsByKey.Values
                    .Where(t => t.Active)
                    .Select(t => t.Friendly ?? $"tgt:{t.TargetId}")
                    .ToList();
                string opts = activeOpts.Count > 0 ? $" Opciones: {string.Join(", ", activeOpts)}" : string.Empty;
                return Result.Fail("No se encontró un monitor activo que coincida." + opts, activeOpts);
            }

            // Validación: no permitir deshabilitar si es el único monitor activo
            int activos = _targetsByKey.Values.Count(t => t.Active);
            if (activos <= 1)
                return Result.Fail("No se puede deshabilitar el único monitor activo");

            // Validación: no permitir deshabilitar si es el monitor primario (posición 0,0)
            string sKey = SKey(match.Value.adapter, match.Value.sourceId);
            if (_sourcesByKey.TryGetValue(sKey, out var srcInfo))
            {
                bool esPrimario = srcInfo.HasMode && srcInfo.PosX == 0 && srcInfo.PosY == 0;
                if (esPrimario)
                {
                    // Sugerir usar setprimary y mostrar opciones (monitores activos) para reasignar primario
                    var activeOpts = _targetsByKey.Values
                        .Where(t => t.Active)
                        .Select(t => t.Friendly ?? $"tgt:{t.TargetId}")
                        .ToList();
                    string opts = activeOpts.Count > 0 ? $" Opciones: {string.Join(", ", activeOpts)}" : string.Empty;
                    return Result.Fail(
                        "No se puede deshabilitar el monitor primario. Cambie el primario primero con 'displayctl setprimary <opción>'." + opts,
                        activeOpts,
                        "Use 'displayctl setprimary <friendly>' para elegir el primario");
                }
            }

            // Construir configuración actual quitando la ruta activa seleccionada
            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                return Result.Fail("Error obteniendo tamaños de buffers");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                return Result.Fail("Error obteniendo configuración actual");

            for (int i = 0; i < pathCount; i++)
            {
                bool isTarget = paths[i].targetInfo.id == match.Value.targetId &&
                                paths[i].sourceInfo.id == match.Value.sourceId &&
                                paths[i].targetInfo.adapterId.LowPart == match.Value.adapter.LowPart &&
                                paths[i].targetInfo.adapterId.HighPart == match.Value.adapter.HighPart;
                if (isTarget)
                {
                    // Quitar ACTIVE
                    paths[i].flags &= ~DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE;
                }
            }

            var activePaths = paths.Where(p => (p.flags & DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE) != 0).ToArray();

            var flags = SDC.USE_SUPPLIED_DISPLAY_CONFIG | SDC.APPLY | SDC.SAVE_TO_DATABASE | SDC.ALLOW_CHANGES;

            int rc = User32DisplayConfig.SetDisplayConfig((uint)activePaths.Length, activePaths, (uint)modes.Length, modes, flags);
            bool ok = rc == 0;
            if (ok) FillData();
            return ok ? Result.Ok() : Result.Fail("SetDisplayConfig falló al deshabilitar");
        }

        public Result SetPrimary(string displayOrName)
        {
            FillData();

            // Resolver el monitor objetivo: por GDI (\\.\DISPLAYx) o por nombre amigable
            SourceInfo? targetSource = null;
            TargetInfo? targetTarget = null;

            bool isGdi = !string.IsNullOrWhiteSpace(displayOrName) && displayOrName.StartsWith("\\\\.\\DISPLAY", StringComparison.OrdinalIgnoreCase);
            if (isGdi)
            {
                targetSource = _sourcesByKey.Values.FirstOrDefault(s => s.GdiName != null && s.GdiName.Equals(displayOrName, StringComparison.OrdinalIgnoreCase));
                if (targetSource == null)
                {
                    var activeOpts = _targetsByKey.Values.Where(t => t.Active).Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                    string opts = activeOpts.Count > 0 ? $" Opciones: {string.Join(", ", activeOpts)}" : string.Empty;
                    return Result.Fail("No se encontró el display especificado." + opts, activeOpts, "Use 'displayctl setprimary <friendly>'");
                }
                if (!targetSource.Active || !targetSource.ActiveTarget.HasValue)
                {
                    var inactiveOpts = _targetsByKey.Values.Where(t => !t.Active).Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                    string opts = inactiveOpts.Count > 0 ? $" Puede activarlo con 'displayctl enable <friendly>'. Opciones: {string.Join(", ", inactiveOpts)}" : string.Empty;
                    return Result.Fail("El monitor debe estar activo para ser primario." + opts, inactiveOpts, "Use 'displayctl enable <friendly>'");
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
                    string opts = activeOpts.Count > 0 ? $" Opciones: {string.Join(", ", activeOpts)}" : string.Empty;
                    return Result.Fail("No se encontró el monitor especificado." + opts, activeOpts, "Use 'displayctl setprimary <friendly>'");
                }
                if (!targetTarget.Active || !targetTarget.ActiveSource.HasValue)
                {
                    var inactiveOpts = _targetsByKey.Values.Where(t => !t.Active).Select(t => t.Friendly ?? $"tgt:{t.TargetId}").ToList();
                    string opts = inactiveOpts.Count > 0 ? $" Puede activarlo con 'displayctl enable <friendly>'. Opciones: {string.Join(", ", inactiveOpts)}" : string.Empty;
                    return Result.Fail("El monitor debe estar activo para ser primario." + opts, inactiveOpts, "Use 'displayctl enable <friendly>'");
                }
                string sKey = SKey(targetTarget.ActiveSource.Value.adapter, targetTarget.ActiveSource.Value.sourceId);
                _sourcesByKey.TryGetValue(sKey, out targetSource);
            }

            if (targetSource == null || targetTarget == null)
                return Result.Fail("No se pudo resolver el monitor objetivo");

            // Si ya es primario (posición 0,0), no hacer nada
            if (targetSource.HasMode && targetSource.PosX == 0 && targetSource.PosY == 0)
                return Result.Ok("El monitor ya era primario");

            // Obtener configuración actual
            if (User32DisplayConfig.GetDisplayConfigBufferSizes(QDC.ALL_PATHS, out uint pathCount, out uint modeCount) != 0)
                return Result.Fail("Error obteniendo tamaños de buffers");

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            if (User32DisplayConfig.QueryDisplayConfig(QDC.ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                return Result.Fail("Error obteniendo configuración actual");

            // Identificar el modo "source" del target y del actual primario (pos 0,0)
            int targetModeIndex = -1;
            var targetAdapter = targetSource.AdapterId;
            uint targetSourceId = targetSource.SourceId;
            // Tomaremos la posición directamente desde "modes" para asegurar coherencia
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
                return Result.Fail("No se encontró el modo del monitor destino");

            // Calcular delta para preservar la estructura: desplazar todos los sources
            // de forma que el monitor objetivo pase a (0,0) y el resto mantenga posiciones relativas.
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

            // Mantener rutas activas como están
            var activePaths = paths.Where(p => (p.flags & DISPLAYCONFIG_PATH_INFO_FLAGS.ACTIVE) != 0).ToArray();
            var flags = SDC.USE_SUPPLIED_DISPLAY_CONFIG | SDC.APPLY | SDC.SAVE_TO_DATABASE | SDC.ALLOW_CHANGES;
            int rc = User32DisplayConfig.SetDisplayConfig((uint)activePaths.Length, activePaths, (uint)modes.Length, modes, flags);
            bool ok = rc == 0;
            if (ok) FillData();
            return ok ? Result.Ok() : Result.Fail("SetDisplayConfig falló al establecer primario");
        }

        public Result SetMonitors(IEnumerable<DesiredMonitor> desiredStates)
        {
            if (desiredStates == null)
                return Result.Fail("Parámetro 'desiredStates' es nulo");

            // Conjunto de nombres deseados habilitados
            var desiredEnabled = new HashSet<string>(
                desiredStates
                    .Where(d => d.Enabled && !string.IsNullOrWhiteSpace(d.Name))
                    .Select(d => d.Name),
                StringComparer.OrdinalIgnoreCase);

            // Pre-validar que todos los deseados existen en el sistema (por nombre amigable)
            var current = List();
            var available = new HashSet<string>(
                current.Select(d => d.FriendlyName).Where(n => !string.IsNullOrWhiteSpace(n))!.Cast<string>(),
                StringComparer.OrdinalIgnoreCase);

            var missing = desiredEnabled.Where(n => !available.Contains(n)).ToList();
            if (missing.Count > 0)
            {
                var opts = available.ToList();
                return Result.Fail(
                    $"No se encontraron los monitores solicitados: {string.Join(", ", missing)}.",
                    opts,
                    "Verifique el nombre y que el monitor esté conectado");
            }

            // 1) Habilitar los monitores deseados
            foreach (var name in desiredEnabled)
            {
                var r = EnableMonitor(name);
                if (!r.Success) return r;
            }

            // Estado tras habilitar
            var afterEnable = List();

            // 2) Asegurar primario sólo si hay monitores deseados
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

            // 3) Deshabilitar los monitores activos que no están en desiredEnabled
            var afterPrimary = List();
            var toDisable = afterPrimary.Where(d => d.IsActive && (string.IsNullOrWhiteSpace(d.FriendlyName) || !desiredEnabled.Contains(d.FriendlyName!)))
                                        .ToList();
            foreach (var d in toDisable)
            {
                // Usar friendly si existe; si no, caer a GDI
                var key = d.FriendlyName ?? d.Active?.GdiName;
                if (string.IsNullOrWhiteSpace(key)) continue;
                var rd = DisableMonitor(key!);
                if (!rd.Success) return rd;
            }

            return Result.Ok("Perfil aplicado");
        }

        public IReadOnlyList<DisplayInfo> List()
        {
            FillData();
            var textScale = GetTextScaleByGdiName();
            var result = new List<DisplayInfo>(_targetsByKey.Count);
            foreach (var t in _targetsByKey.Values.Where(t => t.Available))
            {
                if (t.Active && t.ActiveSource is var asrc && asrc.HasValue)
                {
                    string sk = SKey(t.AdapterId, asrc.Value.sourceId);
                    if (_sourcesByKey.TryGetValue(sk, out var s))
                    {
                        bool isPrimary = s.HasMode && s.PosX == 0 && s.PosY == 0;
                        int? txtScale = null;
                        if (!string.IsNullOrWhiteSpace(s.GdiName) && textScale.TryGetValue(s.GdiName!, out var p)) txtScale = p;
                        // Obtener info de DEVMODE para frecuencia activa, profundidad de color y orientación
                        int? devmodeHz = null;
                        int? bpp = null;
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
                                    if (dm.dmBitsPerPel > 0) bpp = (int)dm.dmBitsPerPel;
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

                        int? bitsPerColor = t.BitsPerColor;
                        if (!bitsPerColor.HasValue && bpp.HasValue)
                        {
                            bitsPerColor = bpp.Value == 30 ? 10 : bpp.Value == 36 ? 12 : bpp.Value >= 24 ? 8 : (int?)null;
                        }

                        var active = new ActiveDetails(
                            s.GdiName,
                            s.PosX,
                            s.PosY,
                            s.Width,
                            s.Height,
                            activeHzOut,
                            orientationStr,
                            t.HasTransform ? t.Scaling.ToString() : null,
                            txtScale,
                            activeHzOut,
                            t.DesktopRefreshHz,
                            t.HdrSupported,
                            t.HdrEnabled,
                            t.ColorEncoding,
                            bitsPerColor,
                            null
                        );
                        result.Add(new DisplayInfo(t.Friendly, true, isPrimary, active));
                        continue;
                    }
                }
                result.Add(new DisplayInfo(t.Friendly, false, false, null));
            }
            return result;
        }
    }
}
