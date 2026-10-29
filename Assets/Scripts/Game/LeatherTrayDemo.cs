using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// Preview helper for the TrayVariants scene: a button that throws a handful of real dice (the same path the Roll screen uses)
    /// into every tray in the scene, so the walls, the collision and the flex can be judged side by side.
    /// </summary>
    public sealed class LeatherTrayDemo : MonoBehaviour
    {
        [SerializeField, Tooltip("Skins for the two dice groups (index 0 and 1).")] private DiceSkin[] skins;
        [SerializeField] private DieKind[] kinds = { DieKind.D20, DieKind.D6, DieKind.D8, DieKind.D12, DieKind.D10, DieKind.D4 };
        [SerializeField, Min(1)] private int diceCount = 10;
        [SerializeField, Tooltip("Height of the button in pixels at a 540 pixel high screen; it scales with the screen.")] private float buttonHeight = 56f;
        [SerializeField] private float referenceScreenHeight = 540f;

        private DiceTray[] _trays;

        private void Start()
        {
            _trays = FindObjectsByType<DiceTray>(FindObjectsSortMode.None);
        }

        private void OnGUI()
        {
            float scale = Screen.height / referenceScreenHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (GUI.Button(new Rect(16f, 16f, 260f, buttonHeight), "Throw " + diceCount + " dice in every tray")) Throw();
        }

        private void Throw()
        {
            if (skins == null || skins.Length == 0) return;
            for (int t = 0; t < _trays.Length; t++)
            {
                var change = new TrayChange { ClearAll = true };
                for (int n = 0; n < diceCount; n++)
                    change.Add.Add(new PhysicalDie(kinds[n % kinds.Length], PhysicalDie.NoTarget, n % 2));
                _trays[t].Apply(change, skins);
            }
        }
    }
}
