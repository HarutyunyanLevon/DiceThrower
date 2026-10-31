# Theme art generator

`theme_art.py` draws the textures and 9-slice UI frames of the themes (Python 3, Pillow, numpy).

    python Tools/ThemeArt/theme_art.py Assets cult cath elf druid tavla     # themes: cult, cath, elf, druid, tavla

Output goes to `Assets/Textures/<Theme>/` and `Assets/UI/<Theme>/`. Everything is seeded, so a run always gives the same pictures;
change a palette in the script and run it again, then run the theme's builder in the Editor (DiceThrower > Themes).
The Dvarven Tavern art was made earlier and is not in this script.

Fonts (OFL) in `Assets/Fonts`, converted from the @fontsource woff files with fontTools (`TTFont(woff).flavor = None; save(ttf)`):
Limelight, Josefin Sans (Dvarven); IM Fell English (SC) (Cultist); Cinzel Decorative, Cormorant Garamond (Cathedral);
Italiana, Jost (Sky Elf Cafe); Bellota Text, Nunito (Druid Cove); Alegreya SC, Alegreya Sans (Armenian Tavla).
