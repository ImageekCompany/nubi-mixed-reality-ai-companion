using System;
using Jolly.XR.Capture;
using UnityEngine;

namespace Jolly.XR.MetaQuest
{
    /// <summary>
    /// Passthrough camera lens pose from Android Camera2 (meta-openxr exposes intrinsics only, xr-stack.md §2.5).
    /// Picks the camera by the vendor key com.meta.extra_metadata.position (0 left, 1 right) and converts
    /// LENS_POSE_TRANSLATION / LENS_POSE_ROTATION with Camera2LensPose. The pose is relative to the gyroscope
    /// (LENS_POSE_REFERENCE = 1 on Quest 3); mapping that to the OpenXR centre eye needs the R1 marker test.
    /// </summary>
    public static class Camera2Extrinsics
    {
        public const int LeftCamera = 0;
        public const int RightCamera = 1;

        /// <summary>Camera id 50 on a Quest 3, Horizon OS v209 (device-spike.md §1), for when Camera2 cannot be read.</summary>
        public static readonly Pose QuestLeftFallback = Camera2LensPose.ToUnity(
            new Vector3(-0.03229f, -0.01753f, -0.06316f), new Quaternion(-0.99527f, 0.00269f, -0.00406f, 0.09704f));

        public static bool TryReadLensPose(int metaPosition, out Pose lensPose, out string error)
        {
            lensPose = default;
            error = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var cameraManager = activity.Call<AndroidJavaObject>("getSystemService", "camera");
                using var classClass = new AndroidJavaClass("java.lang.Class");
                using var floatArrayType = classClass.CallStatic<AndroidJavaObject>("forName", "[F");
                using var integerType = classClass.CallStatic<AndroidJavaObject>("forName", "java.lang.Integer");
                foreach (var id in cameraManager.Call<string[]>("getCameraIdList"))
                {
                    using var characteristics = cameraManager.Call<AndroidJavaObject>("getCameraCharacteristics", id);
                    using var position = Get(characteristics, "com.meta.extra_metadata.position", integerType);
                    if (position == null || position.Call<int>("intValue") != metaPosition)
                        continue;
                    var t = GetFloats(characteristics, "android.lens.poseTranslation", floatArrayType);
                    var r = GetFloats(characteristics, "android.lens.poseRotation", floatArrayType);
                    if (t == null || t.Length < 3 || r == null || r.Length < 4)
                    {
                        error = "camera " + id + " has no lens pose";
                        return false;
                    }
                    lensPose = Camera2LensPose.ToUnity(new Vector3(t[0], t[1], t[2]), new Quaternion(r[0], r[1], r[2], r[3]));
                    return true;
                }
                error = "no camera with position " + metaPosition;
                return false;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
#else
            error = "Camera2 is only available on the Android player";
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Keys are built by name (CameraCharacteristics.Key(String, Class)) so platform and vendor keys share one path.
        static AndroidJavaObject Get(AndroidJavaObject characteristics, string keyName, AndroidJavaObject type)
        {
            using var key = new AndroidJavaObject("android.hardware.camera2.CameraCharacteristics$Key", keyName, type);
            return characteristics.Call<AndroidJavaObject>("get", key);
        }

        static float[] GetFloats(AndroidJavaObject characteristics, string keyName, AndroidJavaObject type)
        {
            using var value = Get(characteristics, keyName, type);
            return value == null ? null : AndroidJNIHelper.ConvertFromJNIArray<float[]>(value.GetRawObject());
        }
#endif
    }
}
