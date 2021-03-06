using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Orange
{
	class DeleteOrphanedTextureParams : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield break; } }
		public IEnumerable<string> BundleExtensions { get { yield return textureParamsExtension; } }

		private readonly string textureParamsExtension = ".texture";

		public int GetOperationCount() => AssetCooker.InputBundle.EnumerateFiles(null, textureParamsExtension).Count();

		public DeleteOrphanedTextureParams(AssetCooker assetCooker) : base(assetCooker) { }

		public void Action()
		{
			foreach (var path in AssetCooker.OutputBundle.EnumerateFiles().ToList()) {
				if (path.EndsWith(textureParamsExtension, StringComparison.OrdinalIgnoreCase)) {
					var origImageFile = Path.ChangeExtension(path, AssetCooker.GetPlatformTextureExtension());
					if (!AssetCooker.OutputBundle.FileExists(origImageFile)) {
						AssetCooker.DeleteFileFromBundle(path);
					}
					UserInterface.Instance.IncreaseProgressBar();
				}
			}
		}
	}
}
