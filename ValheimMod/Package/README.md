# ValheimMod

Replaces Valheim's standard TextMeshPro font assets in place with a custom TMP font
loaded from an AssetBundle.

Place `customfont` beside `ValheimMod.dll`. The bundle must contain a `TMP_FontAsset`
named `MyFont SDF` and should include every required glyph, including Cyrillic glyphs.

The custom font is applied to all loaded TMP font assets and their material presets, as
well as legacy `UnityEngine.UI.Text` and `TextMesh` components. Individual UI layout,
size, alignment, color, and spacing settings are not changed.
