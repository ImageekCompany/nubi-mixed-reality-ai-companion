namespace Jolly.XR.Permissions
{
    public static class XrPermissions
    {
        public const string HeadsetCamera = "horizonos.permission.HEADSET_CAMERA";
        public const string UseScene = "com.oculus.permission.USE_SCENE";
        public const string RecordAudio = "android.permission.RECORD_AUDIO";

        public static readonly string[] All = { HeadsetCamera, UseScene, RecordAudio };
    }
}
