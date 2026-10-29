using UnityEngine;

namespace DiceThrower.Game
{
    public enum LeatherTrayPreset
    {
        /// <summary>Folded leather like a hand-made dice tray: sagging sides, splayed corners, tan binding, stitched edge, brass snaps.</summary>
        FoldedTan = 0,
        /// <summary>Soft rolled rim, round corners, one oiled chestnut tone, a single row of stitches.</summary>
        RolledChestnut = 1,
        /// <summary>Stiff upright box of dark leather, tooled groove, two rows of gold thread, brass rivets.</summary>
        StitchedBox = 2,
        /// <summary>Shallow wine-red dish with two tooled grooves and a black lining.</summary>
        TooledWine = 3,
    }

    /// <summary>Starting values for the shipped tray variants. They are written into NEW style assets only; tune the assets afterwards.</summary>
    public static class LeatherTrayPresets
    {
        public static readonly LeatherTrayPreset[] All =
        {
            LeatherTrayPreset.FoldedTan, LeatherTrayPreset.RolledChestnut, LeatherTrayPreset.StitchedBox, LeatherTrayPreset.TooledWine,
        };

        public static string DisplayName(LeatherTrayPreset p)
        {
            switch (p)
            {
                case LeatherTrayPreset.FoldedTan: return "Folded Tan";
                case LeatherTrayPreset.RolledChestnut: return "Rolled Chestnut";
                case LeatherTrayPreset.StitchedBox: return "Stitched Box";
                default: return "Tooled Wine";
            }
        }

        /// <summary>Asset-friendly name (no spaces).</summary>
        public static string AssetName(LeatherTrayPreset p)
        {
            return p.ToString();
        }

        public static void Fill(LeatherTrayStyle s, LeatherTrayPreset p)
        {
            switch (p)
            {
                case LeatherTrayPreset.FoldedTan:
                    s.cornerRadius = 0.9f; s.wallLength = 1.9f; s.flareDegrees = 18f; s.floorFillet = 0.55f;
                    s.thickness = 0.14f; s.rimBead = 0.05f;
                    s.droop = 0.3f; s.cornerRise = 0.3f; s.cornerFlareDegrees = 14f; s.cornerSharpness = 2f; s.irregularity = 0.05f; s.seed = 11;
                    s.stitchRowsFromRim = new[] { 0.13f };
                    s.rivets = true; s.rivetFromRim = 0.38f; s.rivetRadius = 0.17f;
                    s.topColor = new Color(0.33f, 0.18f, 0.08f); s.liningColor = new Color(0.8f, 0.56f, 0.27f);
                    s.threadColor = new Color(0.93f, 0.84f, 0.62f); s.hardwareColor = new Color(0.85f, 0.62f, 0.24f);
                    s.grainCells = 14; s.pebbleSize = 0.16f; s.grainBump = 0.7f; s.topSmoothness = 0.55f; s.liningSmoothness = 0.12f;
                    s.bounciness = 0.1f; s.dynamicFriction = 0.7f; s.staticFriction = 0.8f;
                    s.flexRadius = 1.7f; s.flexMaxAmplitude = 0.2f; s.flexAmplitudePerSpeed = 0.016f; s.flexFrequency = 9f; s.flexDecaySeconds = 0.28f;
                    break;

                case LeatherTrayPreset.RolledChestnut:
                    s.cornerRadius = 1.9f; s.wallLength = 1.7f; s.flareDegrees = 9f; s.floorFillet = 0.7f;
                    s.thickness = 0.2f; s.rimBead = 0.12f;
                    s.droop = 0f; s.cornerRise = 0f; s.cornerFlareDegrees = 0f; s.irregularity = 0.02f; s.seed = 23;
                    s.stitchRowsFromRim = new[] { 0.2f };
                    s.rivets = false;
                    s.topColor = new Color(0.42f, 0.22f, 0.1f); s.liningColor = new Color(0.5f, 0.3f, 0.15f);
                    s.threadColor = new Color(0.8f, 0.62f, 0.38f);
                    s.grainCells = 16; s.pebbleSize = 0.14f; s.grainBump = 0.65f; s.topSmoothness = 0.62f; s.liningSmoothness = 0.15f;
                    s.bounciness = 0.12f; s.dynamicFriction = 0.65f; s.staticFriction = 0.75f;
                    s.flexRadius = 1.6f; s.flexMaxAmplitude = 0.12f; s.flexAmplitudePerSpeed = 0.011f; s.flexFrequency = 12f; s.flexDecaySeconds = 0.2f;
                    break;

                case LeatherTrayPreset.StitchedBox:
                    s.cornerRadius = 0.6f; s.wallLength = 1.8f; s.flareDegrees = 5f; s.floorFillet = 0.3f;
                    s.thickness = 0.24f; s.rimBead = 0f;
                    s.grooveFromRim = new[] { 0.34f }; s.grooveWidth = 0.14f; s.grooveDepth = 0.05f;
                    s.droop = 0f; s.cornerRise = 0f; s.cornerFlareDegrees = 0f; s.irregularity = 0f; s.seed = 5;
                    s.stitchRowsFromRim = new[] { 0.12f, 0.22f };
                    s.rivets = true; s.rivetFromRim = 0.52f; s.rivetRadius = 0.2f;
                    s.topColor = new Color(0.13f, 0.09f, 0.07f); s.liningColor = new Color(0.2f, 0.13f, 0.08f);
                    s.threadColor = new Color(0.85f, 0.7f, 0.35f); s.hardwareColor = new Color(0.72f, 0.52f, 0.2f);
                    s.grainCells = 18; s.pebbleSize = 0.11f; s.grainBump = 0.8f; s.topSmoothness = 0.42f; s.liningSmoothness = 0.1f;
                    s.bounciness = 0.14f; s.dynamicFriction = 0.6f; s.staticFriction = 0.7f;
                    s.flexRadius = 1.4f; s.flexMaxAmplitude = 0.05f; s.flexAmplitudePerSpeed = 0.005f; s.flexFrequency = 16f; s.flexDecaySeconds = 0.15f;
                    break;

                default: // TooledWine
                    s.cornerRadius = 1.4f; s.wallLength = 1.5f; s.flareDegrees = 24f; s.floorFillet = 0.8f;
                    s.thickness = 0.15f; s.rimBead = 0.06f;
                    s.grooveFromRim = new[] { 0.16f, 0.3f }; s.grooveWidth = 0.12f; s.grooveDepth = 0.045f;
                    s.droop = 0.1f; s.cornerRise = 0.1f; s.cornerFlareDegrees = 6f; s.irregularity = 0.03f; s.seed = 31;
                    s.stitchRowsFromRim = new float[0];
                    s.rivets = false;
                    s.topColor = new Color(0.36f, 0.06f, 0.07f); s.liningColor = new Color(0.1f, 0.07f, 0.06f);
                    s.grainCells = 14; s.pebbleSize = 0.15f; s.grainBump = 0.7f; s.topSmoothness = 0.5f; s.liningSmoothness = 0.1f;
                    s.bounciness = 0.11f; s.dynamicFriction = 0.7f; s.staticFriction = 0.8f;
                    s.flexRadius = 1.7f; s.flexMaxAmplitude = 0.16f; s.flexAmplitudePerSpeed = 0.014f; s.flexFrequency = 10f; s.flexDecaySeconds = 0.25f;
                    break;
            }
        }
    }
}
