using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SkaldicUI
{
	internal static class ModAssetBundle
	{
		internal const string FileName = "skaldicuiassets";

		private static string _pluginLocation;
		private static AssetBundle _bundle;

		internal static void Initialize(string pluginLocation)
		{
			_pluginLocation = pluginLocation;
		}

		internal static bool TryLoad()
		{
			if (_bundle != null)
			{
				return true;
			}

			string pluginDirectory = Path.GetDirectoryName(_pluginLocation);
			if (string.IsNullOrEmpty(pluginDirectory))
			{
				Jotunn.Logger.LogError(
					$"[ModAssetBundle] Could not determine plugin directory from: {_pluginLocation}");
				return false;
			}

			string bundlePath = Path.Combine(pluginDirectory, FileName);
			if (!File.Exists(bundlePath))
			{
				Jotunn.Logger.LogError($"[ModAssetBundle] AssetBundle not found: {bundlePath}");
				return false;
			}

			_bundle = AssetBundle.LoadFromFile(bundlePath);
			if (_bundle == null)
			{
				Jotunn.Logger.LogError($"[ModAssetBundle] Failed to load AssetBundle: {bundlePath}");
				return false;
			}

			Jotunn.Logger.LogInfo($"[ModAssetBundle] Loaded AssetBundle: {bundlePath}");
			return true;
		}

		internal static T LoadAsset<T>(string assetName) where T : UnityEngine.Object
		{
			if (string.IsNullOrWhiteSpace(assetName) || !TryLoad())
			{
				return null;
			}

			T asset = _bundle.LoadAsset<T>(assetName);
			return asset != null
				? asset
				: _bundle.LoadAllAssets<T>().FirstOrDefault(candidate =>
					candidate != null && string.Equals(candidate.name, assetName, StringComparison.Ordinal));
		}

		internal static T[] LoadAllAssets<T>() where T : UnityEngine.Object
		{
			return TryLoad() ? _bundle.LoadAllAssets<T>() : Array.Empty<T>();
		}
	}
}
