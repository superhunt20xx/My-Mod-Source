# SH20XX.BBPP — source (Baldi's Basics Plus 0.14.5)

C# source for the `SH20XX.BBPP` mod.

## Build

```powershell
cd "D:\game\steam\steamapps\common\Baldi's Basics Plus\BALDI_Data\StreamingAssets\Modded\SH20XX.BBPP\Source"
dotnet build -c Release
```

Output: `bin\Release\netstandard2.0\SH20XX.BBPP.dll` → copy to `BepInEx\plugins\`.

Different install location: `dotnet build -c Release -p:GameDir="E:\path\to\Baldi's Basics Plus"`.

## Layout

| Path | Purpose |
| --- | --- |
| `Plugin.cs` | Entry point. Registers content on `LoadingEvents` (Post) and installs the room-injection hook. |
| `ModPaths.cs` | Path helpers + texture/audio cache. |
| `SimpleJson.cs` | Dependency-free JSON reader (tolerates unquoted keys, trailing commas). |
| `Items/` | Item registration + the four `Item` behaviours. |
| `Npcs/NpcRegistration.cs` | Baldi (Corona), Salesperson, YuLuoLin. |
| `Events/` | `RandomEvent` registration + the RoomChao event. |
| `Rooms/RoomRegistration.cs` | Reads `Data/Rooms/**/*.json` into room definitions. |
| `Rooms/RoomBuilder.cs` | Builds real `RoomAsset`s and injects them into level generation. |
| `Rooms/RbplInspector.cs` | Reads the readable header strings of a `.rbpl`. |
| `Rooms/PlusLevelLoaderBridge.cs` | Reflection bridge used for texture aliases and level scenes. |
| `Levels/LevelRegistration.cs` | Registers the `Mezzanine` level type. |
| `Localization/ModLocalization.cs` | Feeds `Localization.json` into `LocalizationManager`. |
| `UI/MainMenuRegistration.cs` | Custom main menu (image + MIDI). |

## Custom warning page

The startup warning page is handled by the separate **CustomWarningPage** plugin
(`BepInEx/plugins/CustomWarningPage/`), not by this mod. It reads
`BepInEx/plugins/CustomWarningPage/Warning.txt` and replaces the game's warning page with
it. When that plugin is installed and has text, this mod skips its own alpha notice
(detected by reflection, so either plugin can be removed independently).

## Item name / description keys

The game resolves `ItemObject.nameKey` / `descKey` through `LocalizationManager`,
so the code keys must match `Localization.json`:

| Item enum | Name key | Description key |
| --- | --- | --- |
| `Taser` | `ITM_Taser` | `ITM_Taser_Desc` |
| `Potion` | `ITM_Potion` | `ITM_Potion_Desc` |
| `99Cassettes` | `ITM_99Cassettes` | `ITM_99Cassettes_Desc` |
| `TestPaper` | `ITM_TestPaper` | `ITM_TestPaper_Desc` |

Missing keys fall back to the hardcoded strings in `ItemRegistration.Register`.

## Rooms: why the .rbpl files are not used

The shipped `.rbpl` files **cannot be decoded**. Verified by calling the game's own
`PlusLevelFormat.LevelExtensions.ReadLevel` at every byte offset of every file —
all of them throw `EndOfStreamException`.

| | Byte layout |
| --- | --- |
| **PlusLevelFormat expects** | `[version][width][height][Int32 nybbleCount][nybbles...][Int32 roomCount]...` |
| **The files actually contain** | `[version][len+name][len+type][len+floor][len+wall][len+ceiling][pad][cell data]...` |

Measured on a working file from another mod (`Room1225.rbpl`):

```
[0]  version = 2
[1]  len=17 'faculty_1_12_null'          <- room name
[19] len=7  'aculty' + 'BlueCarpet'      <- type + texture names
[27] pad byte, then the binary cell data
```

`ReadLevel` treats the name length (`17`) as the width, the next byte as the
height, then reads `17 x N` nybbles — which always runs off the end. This is a
format mismatch, not a parsing bug, and the private cell encoding cannot be
reproduced. These files need re-exporting from a current PlusLevelStudio.

### What the mod does instead

`RoomBuilder` clones a **vanilla** `RoomAsset` whose cell count is closest to the
size in the room JSON, then re-skins the clone with the mod's textures and sets
its name, category, type and spawn weight. That guarantees a valid proven layout.

`Plugin.RegisterRoomInjection` hooks `GeneratorManagement` at the `Base` stage and
appends the built rooms to the `LevelObject`'s `potentialSpecialRooms` and to
matching `roomGroup` entries. The `LevelObject` is cloned first, because
`SceneObject`s are shared assets and mutating the original would leak the rooms
into every later level.

**Known limitation:** the resulting layout is a vanilla room re-skinned with the
mod's textures, not the hand-placed layout in the `.rbpl`. Re-export the `.rbpl`
files and the original layouts can be reconnected.

## Rooms without a layout

`Data/Rooms/Objects/Ruin321` and `Textures/Rooms/TaserRoom` have no usable layout,
so those rooms can only be re-skinned from a vanilla template too.

## PlusLevelLoader

`PlusLevelLoader.dll` (GUID `mtm101.rulerp.baldiplus.levelloader`) hosts
`PlusLevelLoader.PlusLevelLoaderPlugin`. It is probed from `RegisterContent`, not
`Awake` — probing during `Awake` runs before BepInEx has loaded the other plugins
and latches a permanent false negative.
