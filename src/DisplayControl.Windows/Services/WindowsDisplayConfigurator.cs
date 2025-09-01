using System;
using System.Collections.Generic;
using System.Linq;
using DisplayControl.Abstractions;
using DisplayControl.Abstractions.Models;
using DisplayControl.Windows.Helpers;
using DisplayControl.Windows.Interop.User32;

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
            public double RefreshHz;
            public bool HasRefresh;
            public override string ToString() => $"'{Friendly}' [{Vendor}/{ProductHex}] (tgt:{TargetId}) {(Active ? "ACTIVE" : "-")}";
        }

        // índices
        private readonly Dictionary<string, SourceInfo> _sourcesByKey = new();
        private readonly Dictionary<string, TargetInfo> _targetsByKey = new();

        private static string SKey(LUID a, uint sourceId) => $"{a.LowPart}:{a.HighPart}:{sourceId}";
        private static string TKey(LUID a, uint targetId) => $"{a.LowPart}:{a.HighPart}:{targetId}";
        private static bool SameAdapter(LUID a, LUID b) => a.LowPart == b.LowPart && a.HighPart == b.HighPart;

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

                // Frecuencia desde path.targetInfo.refreshRate
                if (p.targetInfo.refreshRate.Denominator != 0)
                {
                    tInfo.RefreshHz = (double)p.targetInfo.refreshRate.Numerator / p.targetInfo.refreshRate.Denominator;
                    tInfo.HasRefresh = true;
                }

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
                    return Result.Fail("No hay monitores disponibles; verifique conexión y encendido.");
                string opts = string.Join(", ", allCandidates.Select(t => t.Friendly ?? $"tgt:{t.TargetId}"));
                return Result.Fail($"No se encontró el monitor especificado. Opciones: {opts}");
            }

            if (friendlyName == null && candidates.Count > 1)
            {
                string opts = string.Join(", ", candidates.Select(t => t.Friendly ?? $"tgt:{t.TargetId}"));
                return Result.Fail($"Hay varios monitores disponibles. Especifique el nombre. Opciones: {opts}");
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
                return Result.Fail("No se encontró un monitor activo que coincida");

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
                    return Result.Fail("No se puede deshabilitar el monitor primario. Cambie el primario primero.");
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
                    return Result.Fail("No se encontró el display especificado");
                if (!targetSource.Active || !targetSource.ActiveTarget.HasValue)
                    return Result.Fail("El monitor debe estar activo para ser primario");
                string tKey = TKey(targetSource.ActiveTarget.Value.adapter, targetSource.ActiveTarget.Value.targetId);
                _targetsByKey.TryGetValue(tKey, out targetTarget);
            }
            else
            {
                targetTarget = _targetsByKey.Values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Friendly) && t.Friendly!.Contains(displayOrName, StringComparison.OrdinalIgnoreCase));
                if (targetTarget == null)
                    return Result.Fail("No se encontró el monitor especificado");
                if (!targetTarget.Active || !targetTarget.ActiveSource.HasValue)
                    return Result.Fail("El monitor debe estar activo para ser primario");
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
            //Encender y apagar monitores según desiredStates
            return Result.Fail("No implementado aún");
        }

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
                        var active = new ActiveDetails(
                            s.GdiName,
                            s.PosX,
                            s.PosY,
                            s.Width,
                            s.Height,
                            t.HasRefresh ? t.RefreshHz : 0.0
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
