using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using LegacyText = UnityEngine.UI.Text;

namespace SkaldicUI
{
	internal static class FontManager
	{
		private const string DefaultTmpReplacementAssetName = "ManuskriptAntiqua-Regular SDF";
		private const string DefaultLegacyReplacementAssetName = "ManuskriptAntiqua-Regular";

		// UseDefault selects DefaultTmpReplacementAssetName, KeepVanilla leaves the game font intact,
		// and FromBundle("Another SDF") selects another asset from skaldicuiassets.
		private static readonly Dictionary<string, FontMapping> TmpFontAssetMappings =
			new Dictionary<string, FontMapping>(StringComparer.Ordinal)
			{
				["Valheim-AveriaSansLibre"] = FontMapping.UseDefault,
				["Valheim-AveriaSerifLibre"] = FontMapping.UseDefault,
				["Valheim-Norse"] = FontMapping.FromBundle("CaesarDressing-Regular SDF"),
				["Valheim-Norsebold"] = FontMapping.FromBundle("CaesarDressing-Regular SDF"),
				["Valheim-Prstartk"] = FontMapping.FromBundle("Signika-Variable SDF"),
				["Valheim-Rune"] = FontMapping.KeepVanilla,
				["Fallback-NotoSansNormal"] = FontMapping.UseDefault,
				["Fallback-NotoSansThin"] = FontMapping.UseDefault,
				["Fallback-NotoSerifNormal"] = FontMapping.UseDefault,
				["NotoEmoji-Light SDF"] = FontMapping.KeepVanilla,
				["NotoEmoji-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansArabic-Light SDF"] = FontMapping.KeepVanilla,
				["NotoSansArabic-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansArmenian-ExtraLight SDF"] = FontMapping.KeepVanilla,
				["NotoSansArmenian-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansBengali-ExtraLight SDF"] = FontMapping.KeepVanilla,
				["NotoSansBengali-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansDevanagari-ExtraLight SDF"] = FontMapping.KeepVanilla,
				["NotoSansDevanagari-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansGeorgian-ExtraLight SDF"] = FontMapping.KeepVanilla,
				["NotoSansGeorgian-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansHebrew-Light SDF"] = FontMapping.KeepVanilla,
				["NotoSansHebrew-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansJP-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansJP-Thin SDF"] = FontMapping.KeepVanilla,
				["NotoSansKR-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansKR-Thin SDF"] = FontMapping.KeepVanilla,
				["NotoSansMalayalam-ExtraLight SDF"] = FontMapping.KeepVanilla,
				["NotoSansMalayalam-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansSC-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSansSC-Thin SDF"] = FontMapping.KeepVanilla,
				["NotoSansThai-ExtraLight SDF"] = FontMapping.KeepVanilla,
				["NotoSansThai-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifArmenian-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifBengali-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifDevanagari-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifGeorgian-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifJP-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifKR-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifMalayalam-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifSC-Regular SDF"] = FontMapping.KeepVanilla,
				["NotoSerifThai-Regular SDF"] = FontMapping.KeepVanilla
			};

		private static readonly Dictionary<string, FontMapping> LegacyFontMappings =
			new Dictionary<string, FontMapping>(StringComparer.Ordinal)
			{
				["AveriaSerifLibre-Regular"] = FontMapping.UseDefault,
				["AveriaSerifLibre-Bold"] = FontMapping.UseDefault,
				["Norse"] = FontMapping.FromBundle("CaesarDressing-Regular"),
				["Norsebold"] = FontMapping.FromBundle("CaesarDressing-Regular")
			};

		private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");

		private static bool _registered;
		private static bool _replacementAssetsLoaded;
		private static readonly Dictionary<string, TMP_FontAsset> BundleTmpFonts =
			new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
		private static readonly Dictionary<string, Font> BundleLegacyFonts =
			new Dictionary<string, Font>(StringComparer.Ordinal);
		private static readonly HashSet<int> PreparedReplacementFontIds = new HashSet<int>();
		private static readonly HashSet<int> BundleFontIds = new HashSet<int>();
		private static readonly HashSet<int> BundleLegacyFontIds = new HashSet<int>();
		private static readonly HashSet<int> ReplacedTmpFontIds = new HashSet<int>();
		private static readonly Dictionary<int, TMP_FontAsset> AtlasReplacements =
			new Dictionary<int, TMP_FontAsset>();

		private enum FontMappingMode
		{
			Default,
			BundleAsset,
			Vanilla
		}

		private readonly struct FontMapping
		{
			internal static readonly FontMapping UseDefault = new FontMapping(FontMappingMode.Default, null);
			internal static readonly FontMapping KeepVanilla = new FontMapping(FontMappingMode.Vanilla, null);

			internal FontMappingMode Mode { get; }
			internal string BundleAssetName { get; }

			private FontMapping(FontMappingMode mode, string bundleAssetName)
			{
				Mode = mode;
				BundleAssetName = bundleAssetName;
			}

			internal static FontMapping FromBundle(string assetName)
			{
				if (string.IsNullOrWhiteSpace(assetName))
				{
					throw new ArgumentException("A bundle font asset name is required", nameof(assetName));
				}

				return new FontMapping(FontMappingMode.BundleAsset, assetName);
			}
		}

		internal static void Initialize()
		{
			if (_registered)
			{
				return;
			}

			_registered = true;
			GUIManager.OnCustomGUIAvailable += OnCustomGuiAvailable;
			SceneManager.sceneLoaded += OnSceneLoaded;
			TryApplyReplacement(logMissingTargets: false);
		}

		private static void OnCustomGuiAvailable()
		{
			TryApplyReplacement(logMissingTargets: true);
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			TryApplyReplacement(logMissingTargets: false);
		}

		private static void TryApplyReplacement(bool logMissingTargets)
		{
			TMP_FontAsset averia = GUIManager.Instance.TMP_AveriaSansLibre;
			TMP_FontAsset norse = GUIManager.Instance.TMP_Norse;

			if (logMissingTargets && (averia == null || norse == null))
			{
				if (averia == null)
				{
					Jotunn.Logger.LogError("[FontManager] Vanilla target TMP_AveriaSansLibre is null");
				}

				if (norse == null)
				{
					Jotunn.Logger.LogError("[FontManager] Vanilla target TMP_Norse is null");
				}
			}

			if (!ModAssetBundle.TryLoad())
			{
				return;
			}

			if (!TryLoadReplacementFonts())
			{
				return;
			}

			if (!TmpFontAssetReflection.Validate())
			{
				return;
			}

			try
			{
				int replacedTmpFonts = ReplaceAllTmpFontAssets(averia, norse);
				int replacedMaterials = ReplaceTmpMaterialPresets();
				int replacedLegacyTexts = ReplaceLegacyTextComponents();

				if (replacedTmpFonts > 0 || replacedMaterials > 0 || replacedLegacyTexts > 0)
				{
					Jotunn.Logger.LogInfo(
						$"[FontManager] Replacement pass completed: TMP fonts={replacedTmpFonts}, " +
						$"TMP material presets={replacedMaterials}, legacy texts={replacedLegacyTexts}");
				}
			}
			catch (Exception exception)
			{
				Jotunn.Logger.LogError($"[FontManager] Font replacement failed: {exception}");
			}
		}

		private static bool TryLoadReplacementFonts()
		{
			if (_replacementAssetsLoaded)
			{
				return true;
			}

			foreach (TMP_FontAsset bundleFont in ModAssetBundle.LoadAllAssets<TMP_FontAsset>())
			{
				if (bundleFont == null)
				{
					continue;
				}

				BundleTmpFonts[bundleFont.name] = bundleFont;
				BundleFontIds.Add(bundleFont.GetInstanceID());
				Jotunn.Logger.LogInfo($"[FontManager] Loaded custom TMP font: {bundleFont.name}");
			}

			foreach (Font bundleFont in ModAssetBundle.LoadAllAssets<Font>())
			{
				if (bundleFont == null)
				{
					continue;
				}

				BundleLegacyFonts[bundleFont.name] = bundleFont;
				BundleLegacyFontIds.Add(bundleFont.GetInstanceID());
				Jotunn.Logger.LogInfo($"[FontManager] Loaded custom legacy font: {bundleFont.name}");
			}

			if (!BundleTmpFonts.ContainsKey(DefaultTmpReplacementAssetName))
			{
				Jotunn.Logger.LogError(
					$"[FontManager] Default TMP replacement '{DefaultTmpReplacementAssetName}' " +
					"was not found in the bundle");
				return false;
			}

			TMP_FontAsset defaultTmpFont = BundleTmpFonts[DefaultTmpReplacementAssetName];
			if (defaultTmpFont.sourceFontFile != null)
			{
				BundleLegacyFonts[defaultTmpFont.sourceFontFile.name] = defaultTmpFont.sourceFontFile;
				BundleLegacyFontIds.Add(defaultTmpFont.sourceFontFile.GetInstanceID());
			}

			if (!BundleLegacyFonts.ContainsKey(DefaultLegacyReplacementAssetName))
			{
				Jotunn.Logger.LogError(
					$"[FontManager] Default legacy replacement '{DefaultLegacyReplacementAssetName}' " +
					"was not found in the bundle");
				return false;
			}

			_replacementAssetsLoaded = true;
			return true;
		}

		private static bool PrepareDynamicFont(TMP_FontAsset font)
		{
			try
			{
				// AssetBundles deserialize character records by glyph index. Rebuild TMP's runtime
				// lookup tables before inspecting or extending the font.
				font.ReadFontAssetDefinition();
				if (font.atlasPopulationMode == AtlasPopulationMode.Static)
				{
					return font.characterTable.Count > 0;
				}

				font.isMultiAtlasTexturesEnabled = true;
				string characters = BuildRuntimeCharacterSet();
				bool addedAll = font.TryAddCharacters(characters, out string missingCharacters, true);
				if (!addedAll && !string.IsNullOrEmpty(missingCharacters))
				{
					Jotunn.Logger.LogWarning(
						$"[FontManager] Custom font '{font.name}' is missing " +
						$"{missingCharacters.Length} requested runtime glyphs: {missingCharacters}");
				}

				Jotunn.Logger.LogInfo(
					$"[FontManager] Prepared dynamic font '{font.name}': " +
					$"glyphs={font.glyphTable.Count}, characters={font.characterTable.Count}, " +
					$"atlases={font.atlasTextures.Length}");
				return font.characterTable.Count > 0;
			}
			catch (Exception exception)
			{
				Jotunn.Logger.LogError($"[FontManager] Could not prepare dynamic font '{font.name}': {exception}");
				return false;
			}
		}

		private static string BuildRuntimeCharacterSet()
		{
			var characters = new StringBuilder();
			AppendUnicodeRange(characters, 0x0020, 0x007E); // Basic Latin
			AppendUnicodeRange(characters, 0x00A0, 0x00FF); // Latin-1 Supplement
			AppendUnicodeRange(characters, 0x0400, 0x052F); // Cyrillic and Cyrillic Supplement
			AppendUnicodeRange(characters, 0x2000, 0x206F); // General Punctuation
			characters.Append('\u20BD'); // Ruble sign
			return characters.ToString();
		}

		private static void AppendUnicodeRange(StringBuilder builder, int first, int last)
		{
			for (int codePoint = first; codePoint <= last; codePoint++)
			{
				builder.Append((char)codePoint);
			}
		}

		private static int ReplaceAllTmpFontAssets(TMP_FontAsset averia, TMP_FontAsset norse)
		{
			var targets = new HashSet<TMP_FontAsset>();
			if (averia != null)
			{
				targets.Add(averia);
			}

			if (norse != null)
			{
				targets.Add(norse);
			}

			foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
			{
				if (font != null && !BundleFontIds.Contains(font.GetInstanceID()))
				{
					targets.Add(font);
				}
			}

			int replaced = 0;
			foreach (TMP_FontAsset target in targets)
			{
				if (ReplacedTmpFontIds.Contains(target.GetInstanceID()))
				{
					continue;
				}

				if (!TryResolveTmpReplacement(target.name, out TMP_FontAsset replacement, out bool keepVanilla))
				{
					if (keepVanilla)
					{
						ReplacedTmpFontIds.Add(target.GetInstanceID());
					}
					continue;
				}

				RememberAtlasMappings(target, replacement);
				LogFontInfo("TMP target", target);
				Jotunn.Logger.LogInfo($"[FontManager] Replacing TMP font: {target.name} <- {replacement.name}");
				ReplaceFontAssetContents(target, replacement);
				TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, target);
				ReplacedTmpFontIds.Add(target.GetInstanceID());
				replaced++;
			}

			return replaced;
		}

		private static bool TryResolveTmpReplacement(
			string vanillaFontName,
			out TMP_FontAsset replacement,
			out bool keepVanilla)
		{
			FontMapping mapping = TmpFontAssetMappings.TryGetValue(vanillaFontName, out FontMapping configured)
				? configured
				: FontMapping.UseDefault;

			keepVanilla = mapping.Mode == FontMappingMode.Vanilla;
			replacement = null;
			if (keepVanilla)
			{
				return false;
			}

			string replacementName = mapping.Mode == FontMappingMode.BundleAsset
				? mapping.BundleAssetName
				: DefaultTmpReplacementAssetName;

			if (!BundleTmpFonts.TryGetValue(replacementName, out replacement))
			{
				Jotunn.Logger.LogError(
					$"[FontManager] Mapping for vanilla TMP font '{vanillaFontName}' references " +
					$"missing bundle font '{replacementName}'");
				return false;
			}

			if (PreparedReplacementFontIds.Add(replacement.GetInstanceID()) && !PrepareDynamicFont(replacement))
			{
				PreparedReplacementFontIds.Remove(replacement.GetInstanceID());
				replacement = null;
				return false;
			}

			return true;
		}

		private static void RememberAtlasMappings(TMP_FontAsset target, TMP_FontAsset replacement)
		{
			if (target.atlasTextures == null)
			{
				return;
			}

			foreach (Texture2D atlas in target.atlasTextures)
			{
				if (atlas != null && atlas != replacement.atlasTexture)
				{
					AtlasReplacements[atlas.GetInstanceID()] = replacement;
				}
			}
		}

		private static int ReplaceTmpMaterialPresets()
		{
			int replaced = 0;
			foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
			{
				if (material == null || !material.HasProperty(MainTextureId))
				{
					continue;
				}

				Texture texture = material.GetTexture(MainTextureId);
				if (texture == null || !AtlasReplacements.TryGetValue(texture.GetInstanceID(), out TMP_FontAsset source))
				{
					continue;
				}

				CopyMaterialContents(material, source);
				replaced++;
			}

			return replaced;
		}

		private static int ReplaceLegacyTextComponents()
		{
			SetGuiManagerLegacyFonts();

			int replaced = 0;
			foreach (LegacyText text in Resources.FindObjectsOfTypeAll<LegacyText>())
			{
				if (text == null || text.font == null)
				{
					continue;
				}

				Font replacement = ResolveLegacyReplacement(text.font);
				if (replacement != null && text.font != replacement)
				{
					text.font = replacement;
					replaced++;
				}
			}

			foreach (TextMesh textMesh in Resources.FindObjectsOfTypeAll<TextMesh>())
			{
				if (textMesh == null || textMesh.font == null)
				{
					continue;
				}

				Font replacement = ResolveLegacyReplacement(textMesh.font);
				if (replacement == null || textMesh.font == replacement)
				{
					continue;
				}

				textMesh.font = replacement;
				Renderer renderer = textMesh.GetComponent<Renderer>();
				if (renderer != null && replacement.material != null)
				{
					renderer.sharedMaterial = replacement.material;
				}
				replaced++;
			}

			return replaced;
		}

		private static void SetGuiManagerLegacyFonts()
		{
			foreach (string propertyName in new[] { "AveriaSerif", "AveriaSerifBold", "Norse", "NorseBold" })
			{
				PropertyInfo property = typeof(GUIManager).GetProperty(
					propertyName,
					BindingFlags.Instance | BindingFlags.Public);
				Font currentFont = property?.GetValue(GUIManager.Instance, null) as Font;
				Font replacement = ResolveLegacyReplacement(currentFont);
				MethodInfo setter = property?.GetSetMethod(nonPublic: true);
				if (replacement != null && currentFont != replacement)
				{
					setter?.Invoke(GUIManager.Instance, new object[] { replacement });
				}
			}
		}

		private static Font ResolveLegacyReplacement(Font vanillaFont)
		{
			if (vanillaFont == null || BundleLegacyFontIds.Contains(vanillaFont.GetInstanceID()))
			{
				return vanillaFont;
			}

			string vanillaFontName = vanillaFont.name;
			FontMapping mapping = LegacyFontMappings.TryGetValue(vanillaFontName, out FontMapping configured)
				? configured
				: FontMapping.UseDefault;
			if (mapping.Mode == FontMappingMode.Vanilla)
			{
				return null;
			}

			string replacementName = mapping.Mode == FontMappingMode.BundleAsset
				? mapping.BundleAssetName
				: DefaultLegacyReplacementAssetName;

			if (BundleLegacyFonts.TryGetValue(replacementName, out Font replacement))
			{
				return replacement;
			}

			Jotunn.Logger.LogError(
				$"[FontManager] Mapping for vanilla legacy font '{vanillaFontName}' references " +
				$"missing bundle font '{replacementName}'");
			return null;
		}

		private static void ReplaceFontAssetContents(TMP_FontAsset target, TMP_FontAsset source)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			if (source.atlasTextures == null || source.atlasTextures.Length == 0 || source.atlasTexture == null)
			{
				throw new InvalidOperationException($"Source font '{source.name}' has no atlas texture");
			}

			if (source.material == null)
			{
				throw new InvalidOperationException($"Source font '{source.name}' has no material");
			}

			UnityEngine.TextCore.FaceInfo vanillaFaceInfo = target.faceInfo;
			target.faceInfo = CreateLayoutCompatibleFaceInfo(vanillaFaceInfo, source.faceInfo);
			target.creationSettings = source.creationSettings;
			target.atlasPopulationMode = source.atlasPopulationMode;
			target.atlasTextures = source.atlasTextures.ToArray();
			target.isMultiAtlasTexturesEnabled = source.isMultiAtlasTexturesEnabled;
			target.getFontFeatures = source.getFontFeatures;
			// Preserve Valheim's fallback chain. Entries mapped to KeepVanilla retain glyph coverage for
			// scripts that the replacement font does not support; replaced entries still update in place.
			List<TMP_FontAsset> vanillaFallbacks = target.fallbackFontAssetTable == null
				? new List<TMP_FontAsset>()
				: new List<TMP_FontAsset>(target.fallbackFontAssetTable);
			target.fallbackFontAssetTable = vanillaFallbacks;

			target.glyphTable.Clear();
			target.glyphTable.AddRange(source.glyphTable);

			// Clone by glyph index rather than by Glyph object. A freshly deserialized AssetBundle
			// may not have linked TMP_Character.glyph yet, while its serialized glyphIndex is valid.
			target.characterTable.Clear();
			foreach (TMP_Character character in source.characterTable)
			{
				if (character == null)
				{
					continue;
				}

				var clone = new TMP_Character
				{
					unicode = character.unicode,
					glyphIndex = character.glyphIndex,
					scale = character.scale
				};
				target.characterTable.Add(clone);
			}

			target.normalStyle = source.normalStyle;
			target.normalSpacingOffset = source.normalSpacingOffset;
			target.boldStyle = source.boldStyle;
			target.boldSpacing = source.boldSpacing;
			target.italicStyle = source.italicStyle;
			target.tabSize = source.tabSize;

			TmpFontAssetReflection.CopyRequiredFields(target, source);
			if (source.atlasPopulationMode != AtlasPopulationMode.Static)
			{
				target.atlasPopulationMode = AtlasPopulationMode.Static;
			}
			CopyMaterialContents(target, source);

			try
			{
				target.ReadFontAssetDefinition();
			}
			catch (Exception exception)
			{
				throw new InvalidOperationException(
					$"ReadFontAssetDefinition failed for target '{target.name}'",
					exception);
			}
		}

		private static UnityEngine.TextCore.FaceInfo CreateLayoutCompatibleFaceInfo(
			UnityEngine.TextCore.FaceInfo vanilla,
			UnityEngine.TextCore.FaceInfo replacement)
		{
			if (vanilla.pointSize <= 0f || replacement.pointSize <= 0f ||
				Mathf.Approximately(replacement.scale, 0f))
			{
				Jotunn.Logger.LogWarning(
					"[FontManager] Could not normalize replacement font layout metrics because " +
					"one of the face point-size/scale values is invalid");
				return replacement;
			}

			// TMP compares ascent - descent against the height of the UI RectTransform before
			// generating any glyph geometry. Several Valheim counters use a 20 px high rect,
			// fixed font size and Truncate overflow. Copying the replacement metrics verbatim
			// can therefore truncate the very first character even though its glyph is valid.
			//
			// Keep the replacement point size and scale (glyph metrics depend on them), but
			// express Valheim's layout metrics in the replacement font's design units. This
			// preserves the normalized on-screen bounds expected by vanilla UI prefabs.
			float layoutUnitScale =
				replacement.pointSize * vanilla.scale /
				(vanilla.pointSize * replacement.scale);

			UnityEngine.TextCore.FaceInfo result = replacement;
			result.lineHeight = vanilla.lineHeight * layoutUnitScale;
			result.ascentLine = vanilla.ascentLine * layoutUnitScale;
			result.capLine = vanilla.capLine * layoutUnitScale;
			result.meanLine = vanilla.meanLine * layoutUnitScale;
			result.baseline = vanilla.baseline * layoutUnitScale;
			result.descentLine = vanilla.descentLine * layoutUnitScale;
			result.superscriptOffset = vanilla.superscriptOffset * layoutUnitScale;
			result.subscriptOffset = vanilla.subscriptOffset * layoutUnitScale;
			return result;
		}

		private static void CopyMaterialContents(TMP_FontAsset target, TMP_FontAsset source)
		{
			Material targetMaterial = target.material;
			if (targetMaterial == null)
			{
				targetMaterial = new Material(source.material)
				{
					name = $"{target.name} Material"
				};
				target.material = targetMaterial;
				Jotunn.Logger.LogWarning(
					$"[FontManager] Target '{target.name}' had no material; created a replacement material");
			}
			// Preserve the vanilla Material object: existing TMP components may reference it directly.
			CopyMaterialContents(targetMaterial, source);
		}

		private static void CopyMaterialContents(Material targetMaterial, TMP_FontAsset source)
		{
			targetMaterial.shader = source.material.shader;
			targetMaterial.CopyPropertiesFromMaterial(source.material);

			if (targetMaterial.HasProperty(MainTextureId))
			{
				targetMaterial.SetTexture(MainTextureId, source.atlasTexture);
			}
			else
			{
				Jotunn.Logger.LogWarning(
					$"[FontManager] Material '{targetMaterial.name}' has no _MainTex property; " +
					"the custom atlas could not be bound explicitly");
			}
		}

		private static void LogFontInfo(string label, TMP_FontAsset font)
		{
			string materialName = font.material != null ? font.material.name : "<null>";
			int glyphCount = font.glyphTable != null ? font.glyphTable.Count : 0;
			int characterCount = font.characterTable != null ? font.characterTable.Count : 0;
			Jotunn.Logger.LogInfo(
				$"[FontManager] {label}: name='{font.name}', glyphs={glyphCount}, " +
				$"characters={characterCount}, atlas={font.atlasWidth}x{font.atlasHeight}, " +
				$"material='{materialName}'");
		}

		private static class TmpFontAssetReflection
		{
			// These serialized fields have no public setters in Valheim's TMP version.
			private static readonly string[] RequiredFieldNames =
			{
				"m_SourceFontFileGUID",
				"m_SourceFontFile",
				"m_SourceFontFilePath",
				"m_AtlasTexture",
				"m_AtlasTextureIndex",
				"m_AtlasWidth",
				"m_AtlasHeight",
				"m_AtlasPadding",
				"m_AtlasRenderMode",
				"m_UsedGlyphRects",
				"m_FreeGlyphRects",
				"m_FontFeatureTable",
				"m_ShouldReimportFontFeatures",
				"m_ClearDynamicDataOnBuild"
			};

			private static Dictionary<string, FieldInfo> _fields;

			internal static bool Validate()
			{
				if (_fields != null)
				{
					return true;
				}

				var fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
				foreach (string fieldName in RequiredFieldNames)
				{
					FieldInfo field = typeof(TMP_FontAsset).GetField(
						fieldName,
						BindingFlags.Instance | BindingFlags.NonPublic);

					if (field == null)
					{
						Jotunn.Logger.LogError(
							$"[FontManager] Expected TMP_FontAsset field '{fieldName}' is missing. " +
							"The installed TextMeshPro version is not supported.");
						return false;
					}

					fields.Add(fieldName, field);
				}

				_fields = fields;
				return true;
			}

			internal static void CopyRequiredFields(TMP_FontAsset target, TMP_FontAsset source)
			{
				foreach (string fieldName in RequiredFieldNames)
				{
					FieldInfo field = _fields[fieldName];
					field.SetValue(target, field.GetValue(source));
				}
			}
		}
	}
}
