using BepInEx;
namespace SkaldicUI
{
	[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	[BepInDependency(Jotunn.Main.ModGuid)]
	internal class SkaldicUI : BaseUnityPlugin
	{
		public const string PluginGUID = "ru.grigorijtomczuk.SkaldicUI";
		public const string PluginName = "Skaldic UI";
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

