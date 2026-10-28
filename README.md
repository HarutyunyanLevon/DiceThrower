# DiceThrower

A D&D dice thrower for mobile (portrait). Unity 6, URP, new Input System, UI Toolkit. Tavern and Leather look.
Physics dice, but the result is decided first by the RNG; the dice tumble and then show that number.

## Run it
1. Open the project, wait for the compile.
2. Menu **DiceThrower > Build Config, Prefabs and Scene** creates anything that is missing (configs, skins, prefabs, scene) and
   fills empty config references on the RollScreen prefab. Existing assets are never overwritten, so tuned values survive.
   To regenerate an asset, delete it and run the menu again.
3. Open `Assets/Scenes/Roll.unity`, set the Game view to a phone (Google Pixel 9 Pro) and press Play.
4. Test Runner > EditMode runs the tests.

## The app
- **Roll tab**: an Attack set and a Damage set, each with its own dice row, modifier (-10..+10), advantage (d20 button),
  and on/off switch. Tap a die to add and throw it, press and slide for a count. Throw (or tap the tray) rethrows everything,
  Clear empties, Save stores the current setup as a preset.
- **Presets tab**: saved setups. Tap one to load it into the Roll tab and throw it; the pencil opens the preset editor, the bin deletes.
- **Preset editor**: the popup for Save and for the pencil. Name, both dice rows, modifiers, advantage and the on/off switches work as on the Roll tab (it reuses `SetPanel` on a `RollSession` of its own). OK needs a name and at least one die.
- **Pinned chips**: a row above the Throw button. Presets used in the last `usageWindow` throws come first (most used first), then the unused ones, newest saved first, at most `pinnedChipsMax`. Tap a chip to load and throw it.
- **History tab**: the last throws, newest first, with time, each set's total and its breakdown. Tap one to throw that setup again.
- **Settings tab**: sound and haptics switches, dice skin, clear history.
- **Sound and haptics**: six cues (throw, die hit, settled, nat 20, nat 1, limit blocked), each a sound plus a vibration, in `FeedbackConfig`. The two switches are read when a cue plays. Vibration uses the Android vibrator service (nothing happens in the Editor; turn on `logCues` to see the cues in the Console).
- A throw is written to the history a moment after the last roll (`historyCommitDelaySeconds`), so tapping three dice quickly is one entry.

## Folders
| Folder | Contents |
|---|---|
| `Scripts/Core` | Pure C#, no Unity references (`DiceThrower.Core`). Dice, rules, rolling, session, and the data layer. |
| `Scripts/Game` | Unity side of the 3D tray: procedural dice, physics, camera fit, feedback (`FeedbackPlayer`, `Haptics`), config assets for them. |
| `Scripts/UI` | UI Toolkit views, the press/slide gestures, UI config assets. |
| `Scripts/Editor` | `ProjectBuilder` (the menu above), `FeedbackAssets` and `PlaceholderAudio` (the placeholder sounds). |
| `Tests/EditMode` | NUnit tests for Core. |
| `UI` | `RollScreen.uxml`, `Tavern.uss` (theme variables only), `Roll.uss` (Roll tab), `Pages.uss` (other tabs and the dialog). |
| `Config` | All tunable numbers and texts as ScriptableObjects. |
| `Prefabs` | `Die`, `DiceTray`, `RollScreen` (carries the `FeedbackPlayer`). |
| `Audio` | The placeholder WAVs. Replace a file (same name) or assign other clips in `FeedbackConfig`. |

## Core
- `DieTypes`, `DicePool` (+ `DiceLimits`), `RollModels`, `RollEngine` (RNG-first rolls, advantage keeps the better or worse pass),
  `DicePlanner` (d% becomes a tens die plus a units die), `Rng`.
- `RollSession`: the live state of the Roll tab and the rules for what a tap, slide or toggle does. It returns a `TrayChange`
  that the 3D tray applies. `Snapshot()` copies the sets for a preset, `Load()` puts a saved setup back (clamped to the limits)
  and throws it, `GetThrow()` copies what is rolled for the history.
- `RollFormat`: text for totals, breakdowns and dice notation, used by all tabs.
- Data layer:
  - `Json`: small JSON reader/writer (Core has no `JsonUtility`).
  - `SaveModels`: `SaveData` = `AppSettings` + `Preset`s + `HistoryEntry`s (newest first).
  - `SaveCodec`: `SaveData` <-> JSON. Enums are written by name; reading is forgiving (missing or unknown parts fall back to defaults);
    malformed text or a newer `version` throws `FormatException`. History stores the dice that were rolled; totals are worked out again on load.
  - `SaveFile`: one file on disk. Writes a temp file and swaps it in. A corrupt file is copied to `.corrupt` and fresh data is used.
  - `AppData`: presets (add, update, delete), history (add, cap, clear) and settings. Every change is saved straight away.
  - `PresetUsage`: which presets are pinned and in what order. `Throttle`: lets an event through once per interval (die hit sounds).
  - A history entry remembers the preset it was thrown from (`PresetId`), which is what the chips count.

The save file is `dicethrower_save.json` in `Application.persistentDataPath`.

## Config assets (`Assets/Config`)
| Asset | What it holds |
|---|---|
| `RulesConfig` | Dice caps, modifier range, **history cap**, **preset name length**, **usage window** and **chip count**. |
| `FeedbackConfig` | Master volume, the six cues (clip, volume, vibration ms and strength), hit sound thresholds, `logCues`. |
| `RollUiConfig` | Set captions and row classes, gesture distances, toast times, Roll tab texts. |
| `DataUiConfig` | Save file name, history commit delay and time format, **dice skin sets**, texts of the Presets, History and Settings tabs and the dialog. |
| `TrayConfig`, `CameraFitConfig`, `DicePhysics` | Tray size and spawning, camera fit, die physics. |
| `Skin_*` | Dice colours. `DataUiConfig` groups them into skin sets (one skin per roll set). |

## Conventions
KISS, DRY, YAGNI. No magic numbers: tunables live in config assets, colours in `Tavern.uss`. Everything is a prefab or asset that can be
adjusted in the Inspector. Core stays free of Unity references and is covered by EditMode tests.
