using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ValheimMod
{
	[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	[BepInDependency(Jotunn.Main.ModGuid)]
	internal class ValheimMod : BaseUnityPlugin
	{
		public const string PluginGUID = "ru.grigorijtomczuk.ValheimMod";
		public const string PluginName = "ValheimMod";
		public const string PluginVersion = "0.1.0";

		private TMP_FontAsset _font;

		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
		private void Awake()
		{
			var bundlePath = System.IO.Path.Combine(
				System.IO.Path.GetDirectoryName(Info.Location)!,
				"customfont"
			);

			var bundle = AssetBundle.LoadFromFile(bundlePath);

			if (bundle == null)
			{
				Jotunn.Logger.LogError("Could not load font AssetBundle");
				return;
			}

			_font = bundle.LoadAsset<TMP_FontAsset>("MyFont SDF");

			if (_font == null)
			{
				Jotunn.Logger.LogError("Could not find TMP font asset");
				return;
			}

			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			ReplaceFonts();
		}

		private void ReplaceFonts()
		{
			foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
			{
				if (text == null)
					continue;

				text.font = _font;
			}
		}
	}
}

