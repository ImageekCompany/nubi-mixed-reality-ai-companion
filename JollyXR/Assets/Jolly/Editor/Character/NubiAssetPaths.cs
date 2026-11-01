namespace Jolly.Editor.Character
{
    public static class NubiAssetPaths
    {
        public const string Model = "Assets/Jolly/Art/Character/Nubi.fbx";
        public const string FacePresets = "Assets/Jolly/Art/Character/Nubi_FacePresets.json";
        public const string Albedo = "Assets/Jolly/Art/Character/Textures/T_Nubi_Albedo.png";

        const string Content = "Assets/Jolly/Content/Character/Nubi/";
        public const string Prefab = Content + "Nubi.prefab";
        public const string Controller = Content + "Animator/Nubi.controller";
        public const string UpperBodyMask = Content + "Animator/NubiUpperBody.mask";
        public const string SkinMaterial = Content + "Materials/M_Nubi_Skin.mat";
        public const string GlossMaterial = Content + "Materials/M_Nubi_Gloss.mat";
        public const string BlobShadowMaterial = Content + "Materials/M_Nubi_BlobShadow.mat";
        public const string SkinShader = "Jolly/Character/NubiSkin";
        public const string BlobShadowShader = "Jolly/Character/BlobShadow";

        public static void EnsureFolderFor(string assetPath) =>
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(assetPath) ?? ".");
    }
}
