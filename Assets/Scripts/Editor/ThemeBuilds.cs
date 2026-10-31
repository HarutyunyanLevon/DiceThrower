using UnityEditor;

namespace DiceThrower.EditorTools
{
    /// <summary>Builds every theme and puts them in the order the theme picker shows them. The saved theme choice is an index into this order.</summary>
    public static class ThemeBuilds
    {
        [MenuItem("DiceThrower/Themes/Build All Themes")]
        public static void BuildAll()
        {
            DvarvenTavernBuilder.Build();
            CultistHideoutBuilder.Build();
            CathedralBuilder.Build();
            ElfCafeBuilder.Build();
            DruidCoveBuilder.Build();
            ArmenianTavlaBuilder.Build();
            ThemeKit.SetThemeOrder(DvarvenTavernBuilder.ThemeName, CultistHideoutBuilder.ThemeName, CathedralBuilder.ThemeName,
                                   ElfCafeBuilder.ThemeName, DruidCoveBuilder.ThemeName, ArmenianTavlaBuilder.ThemeName);
        }
    }
}
