using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using LegacyText = UnityEngine.UI.Text;

namespace ValheimMod
{
	internal static class FontManager
	{
		// The bundle is expected next to the plugin DLL.
		private const string FontBundleName = "customfont";
		private const string RegularFontAssetName = "MyFont SDF";
		private const string NorseFontAssetName = RegularFontAssetName;

		private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");

		private static ManualLogSource _log;
		private static string _pluginLocation;
		private static AssetBundle _bundle;
		private static bool _registered;
		private static TMP_FontAsset _regularReplacement;
		private static TMP_FontAsset _norseReplacement;
		private static Font _legacyReplacement;
		private static bool _replacementFontsPrepared;
		private static readonly HashSet<int> BundleFontIds = new HashSet<int>();
		private static readonly HashSet<int> ReplacedTmpFontIds = new HashSet<int>();
		private static readonly Dictionary<int, TMP_FontAsset> AtlasReplacements =
			new Dictionary<int, TMP_FontAsset>();

		internal static void Initialize(string pluginLocation, ManualLogSource log)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;
			_pluginLocation = pluginLocation;
			_log = log;

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
					_log.LogError("[FontManager] Vanilla target TMP_AveriaSansLibre is null");
				}

				if (norse == null)
				{
					_log.LogError("[FontManager] Vanilla target TMP_Norse is null");
				}
			}

			if (!TryLoadBundle())
			{
				return;
			}

			if (!TryLoadReplacementFonts())
			{
				return;
			}

			if (!TmpFontAssetReflection.Validate(_log))
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
					_log.LogInfo(
						$"[FontManager] Replacement pass completed: TMP fonts={replacedTmpFonts}, " +
						$"TMP material presets={replacedMaterials}, legacy texts={replacedLegacyTexts}");
				}
			}
			catch (Exception exception)
			{
				_log.LogError($"[FontManager] Font replacement failed: {exception}");
			}
		}

		private static bool TryLoadReplacementFonts()
		{
			if (_regularReplacement != null && _norseReplacement != null && _legacyReplacement != null)
			{
				return true;
			}

			_regularReplacement = _regularReplacement ?? LoadFontAsset(RegularFontAssetName);
			_norseReplacement = _norseReplacement ?? (string.Equals(
				NorseFontAssetName,
				RegularFontAssetName,
				StringComparison.Ordinal)
				? _regularReplacement
				: LoadFontAsset(NorseFontAssetName));

			if (_regularReplacement == null || _norseReplacement == null)
			{
				return false;
			}

			if (!_replacementFontsPrepared)
			{
				if (!PrepareDynamicFont(_regularReplacement) ||
					(_norseReplacement != _regularReplacement && !PrepareDynamicFont(_norseReplacement)))
				{
					return false;
				}

				_replacementFontsPrepared = true;
			}

			foreach (TMP_FontAsset bundleFont in _bundle.LoadAllAssets<TMP_FontAsset>())
			{
				if (bundleFont != null)
				{
					BundleFontIds.Add(bundleFont.GetInstanceID());
				}
			}

			_legacyReplacement = _regularReplacement.sourceFontFile;
			if (_legacyReplacement == null)
			{
				_legacyReplacement = _bundle.LoadAllAssets<Font>().FirstOrDefault(font => font != null);
			}

			if (_legacyReplacement == null)
			{
				_log.LogError(
					"[FontManager] The bundle contains no UnityEngine.Font. " +
					"Legacy Unity UI text cannot be replaced.");
				return false;
			}

			_log.LogInfo($"[FontManager] Loaded custom legacy font: {_legacyReplacement.name}");
			return true;
		}

		private static bool PrepareDynamicFont(TMP_FontAsset font)
		{
			if (font.atlasPopulationMode == AtlasPopulationMode.Static || font.characterTable.Count > 0)
			{
				return true;
			}

			try
			{
				font.isMultiAtlasTexturesEnabled = true;
				string characters = BuildRuntimeCharacterSet();
				bool addedAll = font.TryAddCharacters(characters, out string missingCharacters, true);
				if (!addedAll && !string.IsNullOrEmpty(missingCharacters))
				{
					_log.LogWarning(
						$"[FontManager] Custom font '{font.name}' is missing " +
						$"{missingCharacters.Length} requested runtime glyphs: {missingCharacters}");
				}

				_log.LogInfo(
					$"[FontManager] Prepared dynamic font '{font.name}': " +
					$"glyphs={font.glyphTable.Count}, characters={font.characterTable.Count}, " +
					$"atlases={font.atlasTextures.Length}");
				return font.characterTable.Count > 0;
			}
			catch (Exception exception)
			{
				_log.LogError($"[FontManager] Could not prepare dynamic font '{font.name}': {exception}");
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

				TMP_FontAsset replacement = IsNorseFont(target) ? _norseReplacement : _regularReplacement;
				RememberAtlasMappings(target, replacement);
				LogFontInfo("TMP target", target);
				_log.LogInfo($"[FontManager] Replacing TMP font: {target.name} <- {replacement.name}");
				ReplaceFontAssetContents(target, replacement);
				TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, target);
				ReplacedTmpFontIds.Add(target.GetInstanceID());
				replaced++;
			}

			return replaced;
		}

		private static bool IsNorseFont(TMP_FontAsset font)
		{
			return font.name.IndexOf("norse", StringComparison.OrdinalIgnoreCase) >= 0;
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
				if (text != null && text.font != _legacyReplacement)
				{
					text.font = _legacyReplacement;
					replaced++;
				}
			}

			foreach (TextMesh textMesh in Resources.FindObjectsOfTypeAll<TextMesh>())
			{
				if (textMesh == null || textMesh.font == _legacyReplacement)
				{
					continue;
				}

				textMesh.font = _legacyReplacement;
				Renderer renderer = textMesh.GetComponent<Renderer>();
				if (renderer != null && _legacyReplacement.material != null)
				{
					renderer.sharedMaterial = _legacyReplacement.material;
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
				MethodInfo setter = property?.GetSetMethod(nonPublic: true);
				setter?.Invoke(GUIManager.Instance, new object[] { _legacyReplacement });
			}
		}

		private static bool TryLoadBundle()
		{
			if (_bundle != null)
			{
				return true;
			}

			string pluginDirectory = Path.GetDirectoryName(_pluginLocation);
			if (string.IsNullOrEmpty(pluginDirectory))
			{
				_log.LogError($"[FontManager] Could not determine plugin directory from: {_pluginLocation}");
				return false;
			}

			string bundlePath = Path.Combine(pluginDirectory, FontBundleName);
			if (!File.Exists(bundlePath))
			{
				_log.LogError($"[FontManager] AssetBundle not found: {bundlePath}");
				return false;
			}

			_bundle = AssetBundle.LoadFromFile(bundlePath);
			if (_bundle == null)
			{
				_log.LogError($"[FontManager] Failed to load AssetBundle: {bundlePath}");
				return false;
			}

			_log.LogInfo($"[FontManager] Loaded AssetBundle: {bundlePath}");
			// Keep the bundle loaded: its textures and materials are used for the lifetime of the mod.
			return true;
		}

		private static TMP_FontAsset LoadFontAsset(string assetName)
		{
			TMP_FontAsset font = _bundle.LoadAsset<TMP_FontAsset>(assetName);
			if (font == null)
			{
				string availableAssets = string.Join(", ", _bundle.GetAllAssetNames());
				_log.LogError(
					$"[FontManager] TMP_FontAsset '{assetName}' was not found in '{FontBundleName}'. " +
					$"Bundle assets: {availableAssets}");
				return null;
			}

			_log.LogInfo($"[FontManager] Loaded custom TMP font: {font.name}");
			return font;
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

			target.faceInfo = source.faceInfo;
			target.creationSettings = source.creationSettings;
			target.atlasPopulationMode = source.atlasPopulationMode;
			target.atlasTextures = source.atlasTextures.ToArray();
			target.isMultiAtlasTexturesEnabled = source.isMultiAtlasTexturesEnabled;
			target.getFontFeatures = source.getFontFeatures;
			target.fallbackFontAssetTable = source.fallbackFontAssetTable == null
				? new List<TMP_FontAsset>()
				: new List<TMP_FontAsset>(source.fallbackFontAssetTable);

			target.glyphTable.Clear();
			target.glyphTable.AddRange(source.glyphTable);

			// Character entries are mutable: clone them so one replacement can safely feed both targets.
			target.characterTable.Clear();
			foreach (TMP_Character character in source.characterTable)
			{
				var clone = new TMP_Character(character.unicode, character.glyph)
				{
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
				_log.LogWarning(
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
				_log.LogWarning(
					$"[FontManager] Material '{targetMaterial.name}' has no _MainTex property; " +
					"the custom atlas could not be bound explicitly");
			}
		}

		private static void LogFontInfo(string label, TMP_FontAsset font)
		{
			string materialName = font.material != null ? font.material.name : "<null>";
			int glyphCount = font.glyphTable != null ? font.glyphTable.Count : 0;
			int characterCount = font.characterTable != null ? font.characterTable.Count : 0;
			_log.LogInfo(
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
				"m_FontWeightTable",
				"m_ClearDynamicDataOnBuild"
			};

			private static Dictionary<string, FieldInfo> _fields;

			internal static bool Validate(ManualLogSource log)
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
						log.LogError(
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
