using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateAssetBundles
{
	private const string BundleName = "customfont";
	private const string OutputDirectory = "Assets/AssetBundles";

	[MenuItem("Assets/Build AssetBundles")]
	public static void BuildFromMenu()
	{
		Directory.CreateDirectory(OutputDirectory);
		AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
			OutputDirectory,
			BuildAssetBundleOptions.None,
			BuildTarget.StandaloneWindows64);

		string bundlePath = Path.Combine(OutputDirectory, BundleName);
		if (manifest == null || !File.Exists(bundlePath))
		{
			throw new FileNotFoundException(
				$"AssetBundle '{BundleName}' was not produced. Check its asset labels.",
				bundlePath);
		}

		Debug.Log($"Built AssetBundle: {Path.GetFullPath(bundlePath)}");
	}
}
