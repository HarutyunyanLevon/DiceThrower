using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>
    /// The app screen and its wiring. It creates the session, the saved data and the views, and connects them:
    /// a SetPanel gesture changes the RollSession, the resulting TrayChange goes to the 3D tray, and the screen is redrawn.
    /// The pieces live in their own classes: SetPanel (one roll set), TabBar, ToastView, HistoryRecorder, TrayFit, PinnedChips,
    /// the preset editor and the Presets/History/Settings views. Sounds and vibration come from the FeedbackPlayer next to this component.
    /// Numbers and texts come from the config assets, looks from the USS files.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RollScreenView : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private RollUiConfig config;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private CameraFitConfig cameraFit;
        [SerializeField] private DataUiConfig dataConfig;

        [Header("Scene objects (found automatically when left empty)")]
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private DiceTray tray;

        // element names in RollScreen.uxml, in tab order
        private static readonly string[] TabNames = { "tab-roll", "tab-presets", "tab-history", "tab-settings" };
        private static readonly string[] PageNames = { "page-roll", "page-presets", "page-history", "page-settings" };
        private const int TabRoll = 0, TabPresets = 1, TabHistory = 2, TabSettings = 3;
        private const string OpaqueClass = "safe-area--opaque"; // hides the 3D tray behind the list tabs

        private RollSession _session;
        private AppData _data;
        private DiceTray _tray;
        private DiceSkin[] _skins;
        private FeedbackPlayer _feedback;

        private VisualElement _root, _safeArea;
        private SetPanel[] _panels;
        private TabBar _tabs;
        private ToastView _toast;
        private HistoryRecorder _recorder;
        private TrayFit _fit;
        private DialogView _dialog;
        private PresetEditorView _editor;
        private PinnedChips _chips;
        private PresetsView _presets;
        private HistoryView _history;
        private SettingsView _settings;

        // ------------------------------------------------------------------ setup

        private void Start()
        {
            Camera cam = sceneCamera != null ? sceneCamera : Camera.main;
            _tray = tray != null ? tray : FindAnyObjectByType<DiceTray>();
            _feedback = GetComponent<FeedbackPlayer>();
            if (!ConfigIsValid(cam))
            {
                Debug.LogError("RollScreenView: all four configs, a camera, a DiceTray and a FeedbackPlayer with its config are required. "
                               + "The UI config needs exactly " + RollSession.SetCount + " sets and every skin set one skin per roll set. "
                               + "Run the menu DiceThrower > Build Config, Prefabs and Scene to fill in what is missing.", this);
                enabled = false;
                return;
            }

            Application.targetFrameRate = config.targetFrameRate;
            DiceLimits limits = rules.ToLimits();
            _session = new RollSession(new SystemRng(), limits);
            LoadData(limits);

            _root = GetComponent<UIDocument>().rootVisualElement;
            _safeArea = _root.Q<VisualElement>("safe-area");
            VisualElement screen = _root.Q<VisualElement>("screen");
            VisualElement trayArea = _root.Q<VisualElement>("tray-area");

            _toast = new ToastView(_root.Q<Label>("toast"), config.toastSeconds);
            _recorder = new HistoryRecorder(_root, _session, _data, dataConfig.historyCommitDelaySeconds);
            _fit = new TrayFit(_root, _safeArea, trayArea, cam, _tray, cameraFit);
            _tabs = new TabBar(_root, TabNames, PageNames, SelectTab);

            BuildRollTab(new SliderOverlay(screen, config.sliderTrackPad));
            BuildDataTabs(limits);
            PressGesture.Tappable(trayArea, ThrowAll); // tapping the tray rethrows everything

            _feedback.Bind(_data.Settings);
            _tray.DieHit += _feedback.PlayHit;
            _tray.Settled += OnSettled;

            SelectTab(TabRoll);
            Refresh();
        }

        private bool ConfigIsValid(Camera cam)
        {
            return cam != null && _tray != null && _feedback != null && _feedback.HasConfig
                   && config != null && rules != null && cameraFit != null && dataConfig != null
                   && config.sets.Length == RollSession.SetCount && dataConfig.IsValid();
        }

        private void LoadData(DiceLimits limits)
        {
            _data = new AppData(new SaveFile(Path.Combine(Application.persistentDataPath, dataConfig.saveFileName)), limits);
            if (_data.LoadError != null) Debug.LogWarning("DiceThrower: the save file could not be read, starting fresh. " + _data.LoadError.Message, this);
            _data.SaveFailed += e => Debug.LogWarning("DiceThrower: saving failed. " + e.Message, this);
            ApplySkinSet();
        }

        private void Update()
        {
            if (_fit != null) _fit.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _recorder != null) _recorder.Flush();
        }

        private void OnDestroy()
        {
            if (_recorder != null) _recorder.Flush();
            if (_tray != null && _feedback != null)
            {
                _tray.DieHit -= _feedback.PlayHit;
                _tray.Settled -= OnSettled;
            }
        }

        // ------------------------------------------------------------------ building

        private void BuildRollTab(SliderOverlay overlay)
        {
            VisualElement headers = _root.Q<VisualElement>("headers");
            VisualElement rows = _root.Q<VisualElement>("rows");
            _panels = new SetPanel[RollSession.SetCount];
            for (int s = 0; s < _panels.Length; s++)
            {
                int set = s;
                _panels[s] = new SetPanel(s, _session, config, overlay, Apply, (type, result) => ReportBlocked(set, type, result), Refresh);
                headers.Add(_panels[s].Header);
                rows.Add(_panels[s].Row);
            }

            AddAction("btn-clear", LineIcon.Trash, config.clearText, () =>
            {
                _recorder.Flush(); // keep what was thrown before it is cleared away
                Apply(_session.ClearAll());
            });
            AddAction("btn-save", LineIcon.Save, config.saveText, SavePreset);
            PressGesture.Tappable(_root.Q<VisualElement>("btn-throw"), ThrowAll);
        }

        private void AddAction(string elementName, Vector2[][] icon, string text, System.Action onTap)
        {
            VisualElement button = _root.Q<VisualElement>(elementName);
            var glyph = new LineIcon(icon);
            glyph.AddToClassList("action__icon");
            button.Add(glyph);
            button.Add(UiFactory.Label(text, "action__text"));
            PressGesture.Tappable(button, onTap);
        }

        private void BuildDataTabs(DiceLimits limits)
        {
            var captions = new string[RollSession.SetCount];
            for (int s = 0; s < captions.Length; s++) captions[s] = config.sets[s].caption;

            _dialog = new DialogView(_root, dataConfig);
            _editor = new PresetEditorView(_root, config, dataConfig, limits, () => _feedback.Play(FeedbackKind.Blocked));
            _chips = new PinnedChips(_root, _data, rules.usageWindow, rules.pinnedChipsMax, config.slideThresholdPx,
                                     preset => LoadSetup(preset.Sets, preset.Id));
            _recorder.Recorded += _chips.Refresh; // a throw from a preset can change the chip order
            _presets = new PresetsView(_root, dataConfig, captions, config.slideThresholdPx, _data, _dialog,
                                       preset => LoadSetup(preset.Sets, preset.Id), EditPreset);
            _history = new HistoryView(_root, dataConfig, captions, config.slideThresholdPx, _data, RethrowEntry);
            _settings = new SettingsView(_root, dataConfig, _data, _dialog, OnSkinChanged, _toast.Show);
        }

        private void SelectTab(int index)
        {
            _recorder.Flush(); // the History tab must show the throw you just made
            _tabs.Select(index);
            _safeArea.EnableInClassList(OpaqueClass, index != TabRoll);

            if (index == TabRoll) _chips.Refresh(); // presets may have been saved, edited or deleted meanwhile
            else if (index == TabPresets) _presets.Refresh();
            else if (index == TabHistory) _history.Refresh();
            else if (index == TabSettings) _settings.Refresh();
        }

        // ------------------------------------------------------------------ actions

        private void ThrowAll()
        {
            _recorder.Flush(); // the previous throw is finished
            Apply(_session.ThrowAll());
        }

        /// <summary>Loads a saved setup into the Roll tab and throws it. <paramref name="presetId"/> is the preset it came from, or null.</summary>
        private void LoadSetup(IList<RollSetConfig> sets, string presetId)
        {
            _recorder.Flush();
            SelectTab(TabRoll);
            Apply(_session.Load(sets), presetId);
        }

        /// <summary>Throws a history entry's setup again. A set that was off or empty in that throw comes back off.</summary>
        private void RethrowEntry(HistoryEntry entry)
        {
            var sets = new RollSetConfig[RollSession.SetCount];
            for (int s = 0; s < sets.Length; s++)
            {
                SetResult result = entry.Throw.Sets[s];
                sets[s] = result != null ? result.Config : new RollSetConfig { Enabled = false };
            }
            LoadSetup(sets, entry.PresetId);
        }

        private void SavePreset()
        {
            if (!_session.HasDice)
            {
                _toast.Show(dataConfig.saveEmptyToast);
                return;
            }
            string suggestion = string.Format(dataConfig.defaultPresetNameFormat, _data.Presets.Count + 1);
            _editor.Open(dataConfig.savePresetTitle, dataConfig.savePresetOk, suggestion, _session.Snapshot(), (name, sets) =>
            {
                Preset saved = _data.AddPreset(name, sets);
                if (saved == null) return;
                _chips.Refresh();
                _toast.Show(string.Format(dataConfig.presetSavedFormat, saved.Name));
            });
        }

        private void EditPreset(Preset preset)
        {
            _editor.Open(dataConfig.editPresetTitle, dataConfig.editPresetOk, preset.Name, preset.Sets, (name, sets) =>
            {
                _data.UpdatePreset(preset.Id, name, sets);
                _presets.Refresh();
            });
        }

        private void ApplySkinSet()
        {
            int index = Mathf.Clamp(_data.Settings.SkinSet, 0, dataConfig.skinSets.Length - 1);
            _skins = dataConfig.skinSets[index].skins;
        }

        private void OnSkinChanged()
        {
            ApplySkinSet();
            _toast.Show(dataConfig.skinChangedToast);
        }

        private void Apply(TrayChange change)
        {
            Apply(change, null);
        }

        /// <param name="presetId">The preset the throw came from, or null.</param>
        private void Apply(TrayChange change, string presetId)
        {
            if (!change.IsEmpty) _tray.Apply(change, _skins);
            if (change.Add.Count > 0)
            {
                _recorder.Queue(presetId);
                _feedback.Play(FeedbackKind.Throw);
            }
            Refresh();
        }

        /// <summary>All dice lie still: a different cue for a natural 20 or a natural 1.</summary>
        private void OnSettled()
        {
            ThrowResult result = _session.GetThrow();
            FeedbackKind kind = FeedbackKind.Settled;
            if (result != null && result.HasNat20) kind = FeedbackKind.Nat20;
            else if (result != null && result.HasNat1) kind = FeedbackKind.Nat1;
            _feedback.Play(kind);
        }

        private void ReportBlocked(int set, DieType type, AddResult result)
        {
            _toast.Show(result == AddResult.BlockedTotal
                ? string.Format(config.limitTotalFormat, _session.Limits.MaxTotal)
                : string.Format(config.limitTypeFormat, _session.Limits.MaxPerType, DieInfo.Label(type)));
            _panels[set].FlashBadge(type);
            _feedback.Play(FeedbackKind.Blocked);
        }

        private void Refresh()
        {
            for (int s = 0; s < _panels.Length; s++) _panels[s].Refresh();
        }
    }
}
