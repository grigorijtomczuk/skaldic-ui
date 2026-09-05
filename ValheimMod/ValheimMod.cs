using BepInEx;
namespace ValheimMod
{
	[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	[BepInDependency(Jotunn.Main.ModGuid)]
	internal class ValheimMod : BaseUnityPlugin
	{
		public const string PluginGUID = "ru.grigorijtomczuk.ValheimMod";
		public const string PluginName = "ValheimMod";
		public const string PluginVersion = "0.1.0";

		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
		private void Awake()
		{
			ModAssetBundle.Initialize(Info.Location);
			FontManager.Initialize();
			CrosshairManager.Initialize(Config);
		}
	}
}

