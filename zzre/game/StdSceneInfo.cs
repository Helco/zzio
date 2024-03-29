﻿using System.Collections.Generic;
using zzio;

namespace zzre.game;

public static class StdSceneInfo
{
    public readonly record struct Map(UID UID, StdItemId Item);
    public readonly record struct AmbientMode(int Id, bool IsQuiet)
    {
        public override string ToString() => Id.ToString() + (IsQuiet ? "-quiet" : "-normal");
    }

    public static Map? GetMapInfo(string sceneName) =>
        mapInfos.TryGetValue(sceneName, out var map) ? map : null;

    public static AmbientMode? GetAmbientMode(string sceneName) =>
        ambientModes.TryGetValue(sceneName, out var ambient) ? ambient : null;

    public static AmbientMode? GetMusicMode(string sceneName) =>
        musicModes.TryGetValue(sceneName, out var music) ? music : null;

    private static readonly IReadOnlyDictionary<string, Map> mapInfos = new Dictionary<string, Map>()
    {
        { "sc_0202", new(new(0xf54d5721), StdItemId.MapForest) },
        { "sc_0203", new(new(0xf54d5721), StdItemId.MapForest) },
        { "sc_0210", new(new(0xf54d5721), StdItemId.MapForest) },
        { "sc_0211", new(new(0xf54d5721), StdItemId.MapForest) },
        { "sc_0212", new(new(0x6c927721), StdItemId.MapForest) },
        { "sc_0220", new(new(0xf54d5721), StdItemId.MapForest) },
        { "sc_0221", new(new(0xf54d5721), StdItemId.MapForest) },
        { "sc_0230", new(new(0x5167b21), StdItemId.MapForest) },
        { "sc_0231", new(new(0x5bd35321), StdItemId.MapForest) },
        { "sc_0232", new(new(0x5bd35321), StdItemId.MapForest) },
        { "sc_0241", new(new(0xf54d5721), StdItemId.MapForest) },
        { "sc_0242", new(new(0xdcd65721), StdItemId.MapDarkSwamp) },
        { "sc_0400", new(new(0x88787721), StdItemId.MapShadowRealm) },
        { "sc_0611", new(new(0xf1027b21), StdItemId.MapShadowRealm) },
        { "sc_0621", new(new(0xf1027b21), StdItemId.MapShadowRealm) },
        { "sc_0622", new(new(0xf1027b21), StdItemId.MapShadowRealm) },
        { "sc_0623", new(new(0xf1027b21), StdItemId.MapShadowRealm) },
        { "sc_0820", new(new(0xf1027b21), StdItemId.MapShadowRealm) },
        { "sc_0830", new(new(0xf1027b21), StdItemId.MapShadowRealm) },
        { "sc_0840", new(new(0xf1027b21), StdItemId.MapShadowRealm) },
        { "sc_1030", new(new(0xbdccdba1), StdItemId.MapSkyRealm) },
        { "sc_1031", new(new(0xbdccdba1), StdItemId.MapSkyRealm) },
        { "sc_1032", new(new(0xbdccdba1), StdItemId.MapSkyRealm) },
        { "sc_1040", new(new(0xbdccdba1), StdItemId.MapSkyRealm) },
        { "sc_1041", new(new(0x14ef7721), StdItemId.MapSkyRealm) },
        { "sc_1050", new(new(0x4ba67b21), StdItemId.MapSkyRealm) },
        { "sc_1213", new(new(0xdcd65721), StdItemId.MapDarkSwamp) },
        { "sc_1223", new(new(0x9e187721), StdItemId.MapDarkSwamp) },
        { "sc_1233", new(new(0xdcd65721), StdItemId.MapDarkSwamp) },
        { "sc_1234", new(new(0xdcd65721), StdItemId.MapDarkSwamp) },
        { "sc_1242", new(new(0xdcd65721), StdItemId.MapDarkSwamp) },
        { "sc_1243", new(new(0xc0753eb1), StdItemId.MapDarkSwamp) },
        { "sc_1244", new(new(0xdcd65721), StdItemId.MapDarkSwamp) },
        { "sc_1401", new(new(0xdd1e5721), StdItemId.MapMountain) },
        { "sc_1402", new(new(0xdd1e5721), StdItemId.MapMountain) },
        { "sc_1403", new(new(0xe266c611), StdItemId.MapMountain) },
        { "sc_1404", new(new(0xe266c611), StdItemId.MapMountain) },
        { "sc_1410", new(new(0xdd1e5721), StdItemId.MapMountain) },
        { "sc_1412", new(new(0xdd1e5721), StdItemId.MapMountain) },
        { "sc_1413", new(new(0xdd1e5721), StdItemId.MapMountain) },
        { "sc_1422", new(new(0xdd1e5721), StdItemId.MapMountain) },
        { "sc_1432", new(new(0xdd1e5721), StdItemId.MapMountain) },
        { "sc_1623", new(new(0xd8d77eb1), StdItemId.MapMountain) },
        { "sc_1624", new(new(0xd8d77eb1), StdItemId.MapMountain) },
        { "sc_1625", new(new(0xd8d77eb1), StdItemId.MapMountain) },
        { "sc_2201", new(new(0xe266c611), StdItemId.MapMountain) },
        { "sc_2210", new(new(0xe266c611), StdItemId.MapFairyGarden) },
        { "sc_2211", new(new(0x95abe5a1), StdItemId.MapMountain) },
        { "sc_2220", new(new(0x506e9f81), StdItemId.MapMountain) },
        { "sc_2221", new(new(0x506e9f81), StdItemId.MapMountain) },
        { "sc_2222", new(new(0xe266c611), StdItemId.MapMountain) },
        { "sc_2223", new(new(0xba596c01), StdItemId.MapMountain) },
        { "sc_2224", new(new(0x506e9f81), StdItemId.MapMountain) },
        { "sc_2225", new(new(0x506e9f81), StdItemId.MapMountain) },
        { "sc_2226", new(new(0x506e9f81), StdItemId.MapMountain) },
        { "sc_2400", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2401", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2410", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2411", new(new(0x25465321), StdItemId.MapFairyGarden) },
        { "sc_2412", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2420", new(new(0x64ca7721), StdItemId.MapFairyGarden) },
        { "sc_2421", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2430", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2431", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2440", new(new(0x84ab5321), StdItemId.MapFairyGarden) },
        { "sc_2610", new(new(0x64257721), StdItemId.MapMountain) },
        { "sc_2620", new(new(0xdd1e5721), StdItemId.MapMountain) },
    };

    private static readonly IReadOnlyDictionary<string, AmbientMode> ambientModes = new Dictionary<string, AmbientMode>()
    {
        { "sc_0202", new(0, IsQuiet: false) },
        { "sc_0203", new(0, IsQuiet: false) },
        { "sc_0210", new(0, IsQuiet: false) },
        { "sc_0211", new(0, IsQuiet: false) },
        { "sc_0212", new(0, IsQuiet: false) },
        { "sc_0220", new(0, IsQuiet: false) },
        { "sc_0221", new(0, IsQuiet: false) },
        { "sc_0230", new(0, IsQuiet: false) },
        { "sc_0231", new(12, IsQuiet: false) },
        { "sc_0232", new(12, IsQuiet: false) },
        { "sc_0241", new(0, IsQuiet: false) },
        { "sc_0242", new(6, IsQuiet: false) },
        { "sc_0400", new(20, IsQuiet: false) },
        { "sc_0611", new(21, IsQuiet: false) },
        { "sc_0621", new(21, IsQuiet: false) },
        { "sc_0622", new(21, IsQuiet: false) },
        { "sc_0623", new(21, IsQuiet: false) },
        { "sc_0820", new(21, IsQuiet: false) },
        { "sc_0830", new(21, IsQuiet: false) },
        { "sc_0840", new(21, IsQuiet: false) },
        { "sc_1030", new(5, IsQuiet: false) },
        { "sc_1031", new(5, IsQuiet: false) },
        { "sc_1032", new(5, IsQuiet: false) },
        { "sc_1040", new(5, IsQuiet: false) },
        { "sc_1041", new(5, IsQuiet: false) },
        { "sc_1050", new(5, IsQuiet: false) },
        { "sc_1060", new(5, IsQuiet: false) },
        { "sc_1213", new(6, IsQuiet: false) },
        { "sc_1223", new(6, IsQuiet: false) },
        { "sc_1233", new(6, IsQuiet: false) },
        { "sc_1234", new(6, IsQuiet: false) },
        { "sc_1242", new(6, IsQuiet: false) },
        { "sc_1243", new(6, IsQuiet: false) },
        { "sc_1244", new(6, IsQuiet: false) },
        { "sc_1401", new(2, IsQuiet: false) },
        { "sc_1402", new(2, IsQuiet: false) },
        { "sc_1403", new(9, IsQuiet: false) },
        { "sc_1404", new(9, IsQuiet: false) },
        { "sc_1410", new(1, IsQuiet: false) },
        { "sc_1412", new(2, IsQuiet: false) },
        { "sc_1413", new(8, IsQuiet: false) },
        { "sc_1422", new(2, IsQuiet: false) },
        { "sc_1432", new(2, IsQuiet: false) },
        { "sc_1623", new(3, IsQuiet: false) },
        { "sc_1624", new(3, IsQuiet: false) },
        { "sc_1625", new(3, IsQuiet: false) },
        { "sc_2201", new(9, IsQuiet: false) },
        { "sc_2210", new(4, IsQuiet: false) },
        { "sc_2211", new(9, IsQuiet: false) },
        { "sc_2220", new(23, IsQuiet: false) },
        { "sc_2221", new(23, IsQuiet: false) },
        { "sc_2222", new(4, IsQuiet: false) },
        { "sc_2223", new(4, IsQuiet: false) },
        { "sc_2224", new(23, IsQuiet: false) },
        { "sc_2225", new(23, IsQuiet: false) },
        { "sc_2226", new(23, IsQuiet: false) },
        { "sc_2400", new(1, IsQuiet: false) },
        { "sc_2401", new(1, IsQuiet: false) },
        { "sc_2410", new(1, IsQuiet: false) },
        { "sc_2411", new(1, IsQuiet: false) },
        { "sc_2412", new(1, IsQuiet: false) },
        { "sc_2420", new(1, IsQuiet: false) },
        { "sc_2421", new(1, IsQuiet: false) },
        { "sc_2430", new(1, IsQuiet: false) },
        { "sc_2431", new(1, IsQuiet: false) },
        { "sc_2440", new(1, IsQuiet: false) },
        { "sc_2610", new(8, IsQuiet: false) },
        { "sc_2620", new(8, IsQuiet: false) },
        { "sc_2800", new(7, IsQuiet: false) },
        { "sc_2801", new(7, IsQuiet: false) },
        { "sc_3010", new(24, IsQuiet: false) },
        { "sc_3011", new(24, IsQuiet: false) },
        { "sc_3012", new(21, IsQuiet: false) },
        { "sc_3200", new(10, IsQuiet: false) },
        { "sc_3201", new(10, IsQuiet: false) },
        { "sc_3202", new(10, IsQuiet: false) },
        { "sc_3203", new(10, IsQuiet: false) },
        { "sc_3204", new(10, IsQuiet: false) },
        { "sc_3250", new(11, IsQuiet: false) },
        { "sc_3251", new(11, IsQuiet: false) },
        { "sc_3252", new(11, IsQuiet: false) },
        { "sc_3253", new(11, IsQuiet: false) },
        { "sc_3254", new(11, IsQuiet: false) },
        { "sc_3300", new(13, IsQuiet: false) },
        { "sc_3301", new(13, IsQuiet: false) },
        { "sc_3302", new(13, IsQuiet: false) },
        { "sc_3303", new(13, IsQuiet: false) },
        { "sc_3304", new(14, IsQuiet: false) },
        { "sc_3350", new(11, IsQuiet: false) },
        { "sc_3351", new(11, IsQuiet: false) },
        { "sc_3352", new(11, IsQuiet: false) },
        { "sc_3353", new(11, IsQuiet: false) },
        { "sc_3354", new(11, IsQuiet: false) },
        { "sc_3400", new(16, IsQuiet: false) },
        { "sc_3401", new(16, IsQuiet: false) },
        { "sc_3402", new(16, IsQuiet: false) },
        { "sc_3403", new(16, IsQuiet: false) },
        { "sc_3404", new(16, IsQuiet: false) },
        { "sc_3405", new(16, IsQuiet: false) },
        { "sc_3406", new(19, IsQuiet: false) },
        { "sc_3450", new(17, IsQuiet: false) },
        { "sc_3451", new(17, IsQuiet: false) },
        { "sc_3452", new(17, IsQuiet: false) },
        { "sc_3453", new(17, IsQuiet: false) },
        { "sc_3454", new(8, IsQuiet: false) },
        { "sc_3455", new(18, IsQuiet: false) },
        { "sc_3500", new(15, IsQuiet: false) },
        { "sc_3600", new(22, IsQuiet: false) },
    };

    private static readonly IReadOnlyDictionary<string, AmbientMode> musicModes = new Dictionary<string, AmbientMode>()
    {
        { "sc_0231", new(2, IsQuiet: false) },
        { "sc_0232", new(2, IsQuiet: false) },
        { "sc_1243", new(3, IsQuiet: false) },
        { "sc_2211", new(4, IsQuiet: false) },
        { "sc_2411", new(1, IsQuiet: false) },
        { "sc_2421", new(1, IsQuiet: true) },
        { "sc_3200", new(1, IsQuiet: true) },
        { "sc_3201", new(1, IsQuiet: true) },
        { "sc_3202", new(1, IsQuiet: true) },
        { "sc_3204", new(1, IsQuiet: true) },
        { "sc_3250", new(3, IsQuiet: true) },
        { "sc_3251", new(3, IsQuiet: true) },
        { "sc_3252", new(3, IsQuiet: true) },
        { "sc_3253", new(3, IsQuiet: true) },
        { "sc_3254", new(3, IsQuiet: true) },
        { "sc_3300", new(2, IsQuiet: true) },
        { "sc_3302", new(2, IsQuiet: true) },
        { "sc_3303", new(2, IsQuiet: true) },
        { "sc_3304", new(2, IsQuiet: true) },
        { "sc_3400", new(4, IsQuiet: true) },
        { "sc_3401", new(4, IsQuiet: true) },
        { "sc_3402", new(4, IsQuiet: true) },
        { "sc_3403", new(4, IsQuiet: true) },
        { "sc_3404", new(4, IsQuiet: true) },
        { "sc_3405", new(4, IsQuiet: true) },
        { "sc_3406", new(4, IsQuiet: true) },
    };
}
