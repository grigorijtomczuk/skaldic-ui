using BepInEx.Configuration;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimMod
{
	internal sealed class CrosshairManager : MonoBehaviour
	{
		private static ConfigEntry<bool> _enabled;
		private static ConfigEntry<string> _spriteAssetName;
		private static ConfigEntry<string> _bowSpriteAssetName;
		private static ConfigEntry<float> _sizeMultiplier;
		private static ConfigEntry<bool> _overrideColor;
		private static ConfigEntry<string> _color;
		private static ConfigEntry<bool> _applyToBowCrosshair;
		private static ConfigEntry<bool> _stealthEnabled;
		private static ConfigEntry<string> _stealthSpriteAssetName;
		private static ConfigEntry<string> _stealthHiddenSpriteAssetName;
		private static ConfigEntry<string> _stealthTargetedSpriteAssetName;
		private static ConfigEntry<string> _stealthAlertSpriteAssetName;
		private static ConfigEntry<float> _stealthSizeMultiplier;
		private static ConfigEntry<bool> _stealthOverrideColor;
		private static ConfigEntry<string> _stealthColor;
		private static bool _initialized;

		private readonly ImageState _regular = new ImageState();
		private readonly ImageState _bow = new ImageState();
		private readonly ImageState _stealthHidden = new ImageState();
		private readonly ImageState _stealthTargeted = new ImageState();
		private readonly ImageState _stealthAlert = new ImageState();
		private Sprite _regularSprite;
		private Sprite _bowSprite;
		private Sprite _stealthSprite;
		private Sprite _stealthHiddenSprite;
		private Sprite _stealthTargetedSprite;
		private Sprite _stealthAlertSprite;
		private string _loadedRegularSpriteName;
		private string _loadedBowSpriteName;
		private string _loadedStealthSpriteName;
		private string _loadedStealthHiddenSpriteName;
		private string _loadedStealthTargetedSpriteName;
		private string _loadedStealthAlertSpriteName;
		private bool _warnedAboutColor;
		private bool _warnedAboutStealthColor;

		internal static void Initialize(ConfigFile config)
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;
			_enabled = config.Bind(
				"Crosshair",
				"Enabled",
				true,
				"Enable custom crosshair settings.");
			_spriteAssetName = config.Bind(
				"Crosshair",
				"SpriteAssetName",
				"Crosshair",
				"Sprite asset name inside valheimmodassets. Empty keeps the vanilla crosshair sprite.");
			_bowSpriteAssetName = config.Bind(
				"Crosshair",
				"BowSpriteAssetName",
				"BowCrosshair",
				"Sprite asset name for the bow crosshair. Empty uses SpriteAssetName.");
			_sizeMultiplier = config.Bind(
				"Crosshair",
				"SizeMultiplier",
				1f,
				new ConfigDescription(
					"Crosshair size relative to the vanilla HUD size.",
					new AcceptableValueRange<float>(0.1f, 10f)));
			_overrideColor = config.Bind(
				"Crosshair",
				"OverrideColor",
				false,
				"Override crosshair RGB while preserving visibility controlled by the game.");
			_color = config.Bind(
				"Crosshair",
				"Color",
				"#FFFFFF",
				"HTML RGB color, for example #FF4040.");
			_applyToBowCrosshair = config.Bind(
				"Crosshair",
				"ApplyToBowCrosshair",
				true,
				"Apply the same settings to Hud.m_crosshairBow.");
			_stealthEnabled = config.Bind(
				"StealthIndicator",
				"Enabled",
				true,
				"Enable custom stealth-eye settings.");
			_stealthSpriteAssetName = config.Bind(
				"StealthIndicator",
				"SpriteAssetName",
				"StealthTargeted",
				"Sprite used for every stealth state. Empty keeps vanilla sprites.");
			_stealthHiddenSpriteAssetName = config.Bind(
				"StealthIndicator",
				"HiddenSpriteAssetName",
				"StealthHidden",
				"Optional hidden-state override. Empty uses SpriteAssetName.");
			_stealthAlertSpriteAssetName = config.Bind(
				"StealthIndicator",
				"AlertSpriteAssetName",
				"StealthAlert",
				"Optional alerted-state override. Empty uses SpriteAssetName.");
			_stealthTargetedSpriteAssetName = config.Bind(
				"StealthIndicator",
				"TargetedSpriteAssetName",
				"StealthTargeted",
				"Optional detected-state override. Empty uses SpriteAssetName.");
			_stealthSizeMultiplier = config.Bind(
				"StealthIndicator",
				"SizeMultiplier",
				1f,
				new ConfigDescription(
					"Stealth-eye size relative to the vanilla HUD size.",
					new AcceptableValueRange<float>(0.1f, 10f)));
			_stealthOverrideColor = config.Bind(
				"StealthIndicator",
				"OverrideColor",
				false,
				"Override stealth-eye RGB while preserving visibility controlled by the game.");
			_stealthColor = config.Bind(
				"StealthIndicator",
				"Color",
				"#FFFFFF",
				"HTML RGB color, for example #FF4040.");

			var managerObject = new GameObject("ValheimMod.CrosshairManager");
			DontDestroyOnLoad(managerObject);
			managerObject.hideFlags = HideFlags.HideAndDontSave;
			managerObject.AddComponent<CrosshairManager>();
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
		private void LateUpdate()
		{
			Hud hud = Hud.instance;
			if (hud == null)
			{
				return;
			}

			_regular.Capture(hud.m_crosshair);
			_bow.Capture(hud.m_crosshairBow);
			_stealthHidden.Capture(FindImage(hud.m_hidden));
			_stealthTargeted.Capture(FindImage(hud.m_targeted));
			_stealthAlert.Capture(FindImage(hud.m_targetedAlert));

			ApplyCrosshair();
			ApplyStealthIndicator();
		}

		private void ApplyCrosshair()
		{
			if (!_enabled.Value)
			{
				_regular.Restore();
				_bow.Restore();
				return;
			}

			EnsureCrosshairSpritesLoaded();
			Color? color = TryGetConfiguredColor(
				_overrideColor,
				_color,
				ref _warnedAboutColor,
				"CrosshairManager");
			_regular.Apply(_regularSprite, _sizeMultiplier.Value, color);

			if (_applyToBowCrosshair.Value)
			{
				_bow.Apply(_bowSprite ?? _regularSprite, _sizeMultiplier.Value, color);
			}
			else
			{
				_bow.Restore();
			}
		}

		private void ApplyStealthIndicator()
		{
			if (!_stealthEnabled.Value)
			{
				_stealthHidden.Restore();
				_stealthTargeted.Restore();
				_stealthAlert.Restore();
				return;
			}

			EnsureStealthSpritesLoaded();
			Color? color = TryGetConfiguredColor(
				_stealthOverrideColor,
				_stealthColor,
				ref _warnedAboutStealthColor,
				"StealthIndicator");
			_stealthHidden.Apply(_stealthHiddenSprite ?? _stealthSprite, _stealthSizeMultiplier.Value, color);
			_stealthTargeted.Apply(_stealthTargetedSprite ?? _stealthSprite, _stealthSizeMultiplier.Value, color);
			_stealthAlert.Apply(_stealthAlertSprite ?? _stealthSprite, _stealthSizeMultiplier.Value, color);
		}

		private void EnsureCrosshairSpritesLoaded()
		{
			string regularName = _spriteAssetName.Value?.Trim() ?? string.Empty;
			if (!string.Equals(regularName, _loadedRegularSpriteName, StringComparison.Ordinal))
			{
				_regularSprite = LoadSprite(regularName);
				_loadedRegularSpriteName = regularName;
			}

			string bowName = _bowSpriteAssetName.Value?.Trim() ?? string.Empty;
			if (!string.Equals(bowName, _loadedBowSpriteName, StringComparison.Ordinal))
			{
				_bowSprite = LoadSprite(bowName);
				_loadedBowSpriteName = bowName;
			}
		}

		private void EnsureStealthSpritesLoaded()
		{
			LoadSpriteIfChanged(
				_stealthSpriteAssetName.Value,
				ref _loadedStealthSpriteName,
				ref _stealthSprite);
			LoadSpriteIfChanged(
				_stealthHiddenSpriteAssetName.Value,
				ref _loadedStealthHiddenSpriteName,
				ref _stealthHiddenSprite);
			LoadSpriteIfChanged(
				_stealthTargetedSpriteAssetName.Value,
				ref _loadedStealthTargetedSpriteName,
				ref _stealthTargetedSprite);
			LoadSpriteIfChanged(
				_stealthAlertSpriteAssetName.Value,
				ref _loadedStealthAlertSpriteName,
				ref _stealthAlertSprite);
		}

		private static void LoadSpriteIfChanged(string configuredName, ref string loadedName, ref Sprite sprite)
		{
			string assetName = configuredName?.Trim() ?? string.Empty;
			if (string.Equals(assetName, loadedName, StringComparison.Ordinal))
			{
				return;
			}

			sprite = LoadSprite(assetName);
			loadedName = assetName;
		}

		private static Sprite LoadSprite(string assetName)
		{
			if (string.IsNullOrEmpty(assetName))
			{
				return null;
			}

			Sprite sprite = ModAssetBundle.LoadAsset<Sprite>(assetName);
			if (sprite == null)
			{
				Jotunn.Logger.LogError(
					$"[CrosshairManager] Sprite '{assetName}' was not found in {ModAssetBundle.FileName}");
			}
			else
			{
				Jotunn.Logger.LogInfo($"[CrosshairManager] Loaded crosshair sprite: {sprite.name}");
			}

			return sprite;
		}

		private static Color? TryGetConfiguredColor(
			ConfigEntry<bool> overrideColor,
			ConfigEntry<string> configuredColor,
			ref bool warned,
			string logLabel)
		{
			if (!overrideColor.Value)
			{
				warned = false;
				return null;
			}

			if (ColorUtility.TryParseHtmlString(configuredColor.Value, out Color color))
			{
				warned = false;
				return color;
			}

			if (!warned)
			{
				Jotunn.Logger.LogError($"[{logLabel}] Invalid HTML color: '{configuredColor.Value}'");
				warned = true;
			}

			return null;
		}

		private static Image FindImage(GameObject root)
		{
			return root == null
				? null
				: root.GetComponent<Image>() ?? root.GetComponentInChildren<Image>(includeInactive: true);
		}

		private sealed class ImageState
		{
			private Image _image;
			private Sprite _originalSprite;
			private Vector2 _originalSize;
			private Color _originalColor;
			private bool _isApplied;

			internal void Capture(Image image)
			{
				if (_image == image)
				{
					return;
				}

				_image = image;
				_isApplied = false;
				if (_image == null)
				{
					return;
				}

				_originalSprite = _image.sprite;
				_originalSize = _image.rectTransform.sizeDelta;
				_originalColor = _image.color;
			}

			internal void Apply(Sprite sprite, float sizeMultiplier, Color? configuredColor)
			{
				if (_image == null)
				{
					return;
				}

				_image.sprite = sprite ?? _originalSprite;
				_image.rectTransform.sizeDelta = _originalSize * sizeMultiplier;
				if (configuredColor.HasValue)
				{
					Color current = _image.color;
					Color configured = configuredColor.Value;
					_image.color = new Color(configured.r, configured.g, configured.b, current.a);
				}

				_isApplied = true;
			}

			internal void Restore()
			{
				if (_image == null || !_isApplied)
				{
					return;
				}

				_image.sprite = _originalSprite;
				_image.rectTransform.sizeDelta = _originalSize;
				_image.color = _originalColor;
				_isApplied = false;
			}
		}
	}
}
