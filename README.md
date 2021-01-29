![GBX.NET](logo_outline.png)

[![Nuget](https://img.shields.io/nuget/v/GBX.NET?style=for-the-badge)](https://www.nuget.org/packages/GBX.NET/)
[![GitHub release (latest by date including pre-releases)](https://img.shields.io/github/v/release/BigBang1112/gbx-net?include_prereleases&style=for-the-badge)](https://github.com/BigBang1112/gbx-net/releases)
[![GitHub code size in bytes](https://img.shields.io/github/languages/code-size/BigBang1112/gbx-net?style=for-the-badge)](#)
[![GitHub](https://img.shields.io/github/license/BigBang1112/gbx-net?style=for-the-badge)](https://github.com/BigBang1112/gbx-net/blob/master/LICENSE)

GBX.NET is a GameBox (.Gbx) file parser library written in C# for .NET software framework. This file type can be seen in many of the Nadeo games like TrackMania, ShootMania or Virtual Skipper.

- GBX.NET can recognize **entire GBX files**, however **can't read all of the possible files**. GBX file is basically a serialized class from the GameBox engine, and all of these classes must be known to read. This is where you can help contributing to the project, by [exploring new chunks](https://github.com/BigBang1112/gbx-net/wiki/How-to-discover-nodes-and-chunks) (available very soon).
- GBX.NET can write GBX files which can be read by the parser, however this may not apply to all readable GBXs.
- All versions of GBX are supported: ranging from TM1.0 to TM®.
- Reading PAK file isn't currently supported.

| Extension | Node | Can read | Can write
| --- | --- | --- | ---
| Map.Gbx | [CGameCtnChallenge](GBX.NET/Engines/Game/CGameCtnChallenge.cs) | Yes | Yes
| Replay.Gbx | [CGameCtnReplayRecord](GBX.NET/Engines/Game/CGameCtnReplayRecord.cs) | Yes | No
| Ghost.Gbx | [CGameCtnGhost](GBX.NET/Engines/Game/CGameCtnGhost.cs) | Yes | Yes
| Clip.Gbx | [CGameCtnMediaClip](GBX.NET/Engines/Game/CGameCtnMediaClip.cs) | Yes | Yes
| EDClassic.Gbx | [CGameCtnBlockInfoClassic](GBX.NET/Engines/Game/CGameCtnBlockInfoClassic.cs) | Yes | No
| Campaign.Gbx | [CGameCtnCampaign](GBX.NET/Engines/Game/CGameCtnCampaign.cs) | Yes | Yes
| Block.Gbx | [CGameItemModel](GBX.NET/Engines/GameData/CGameItemModel.cs) | Yes | No
| Macroblock.Gbx | [CGameCtnMacroBlockInfo](GBX.NET/Engines/Game/CGameCtnMacroBlockInfo.cs) | Yes | No
| Item.Gbx | [CGameItemModel](GBX.NET/Engines/GameData/CGameItemModel.cs) | Yes | No

## Compatibility

GBX.NET is compatible with **.NET Standard 2.0** since version 0.2.0. Earlier versions are built on .NET Standard 2.1.

Since version 0.5.2, the library is also compatible with **.NET Framework 4.5**.

The library is also set to **x86 assembly** due to LZO compression problems in x64. It is unsure whenever this will be resolved.

## Dependencies

### GBX.NET

- 0.0.1+
  - System.Drawing.Common

#### Obsolete

- 0.0.1 - 0.4.1: SharpZipLib.NETStandard
- 0.1.0 - 0.4.1: Microsoft.CSharp

### GBX.NET.Json
- Newtonsoft.Json

## Usage

To parse a GBX with a known type:

```cs
var gbx = GameBox.Parse<CGameCtnChallenge>("MyMap.Map.Gbx");
// Node data is available in gbx.MainNode
```

To parse a GBX with an unknown type:

```cs
var gbx = GameBox.Parse("MyMap.Map.Gbx");

if (gbx is GameBox<CGameCtnChallenge> gbxMap)
{
    // Node data is available in gbxMap.MainNode
}
else if (gbx is GameBox<CGameCtnReplayRecord> gbxReplay)
{
    // Node data is available in gbxReplay.MainNode
}
```

## License

The source code files fall under [GNU General Public License v3.0](LICENSE). Information gathered from the project (chunk structure, parse examples, data structure, wiki information, markdown) is usable with [The Unlicense](https://unlicense.org/).

Font of the logo is called [Raleway](https://fonts.google.com/specimen/Raleway) licensed under [SIL Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL). You can use the logo as a part of your media.

## Alternative GBX parsers

- [gbx.js](https://github.com/ThaumicTom/gbx.js) (GBX header parser for clientside JavaScript)
- [ManiaPlanetSharp](https://github.com/stefan-baumann/ManiaPlanetSharp) (C# toolkit for accessing ManiaPlanet data, including GBX parser used by ManiaExchange)
