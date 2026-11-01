using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace Jolly.Spikes
{
    /// <summary>Spike (b): passthrough CPU image via meta-openxr, intrinsics, JPEG encode.</summary>
    static class CameraProbe
    {
        const int MaxUploadSide = 640;

        public static async UniTask<byte[]> RunAsync(ARCameraManager manager, CancellationToken ct)
        {
            var json = new JObject();
            var waitStart = SpikeLog.NowMs;
            var subsystemTimedOut = await UniTask
                .WaitUntil(() => manager.subsystem != null && manager.subsystem.running, cancellationToken: ct)
                .TimeoutWithoutException(TimeSpan.FromSeconds(10));
            json["subsystemRunning"] = !subsystemTimedOut;
            json["subsystemType"] = manager.subsystem?.GetType().FullName;
            json["permissionGranted"] = manager.permissionGranted;

            var meta = manager.subsystem as MetaOpenXRCameraSubsystem;
            if (meta != null)
            {
                json["availableCameras"] = meta.GetAvailableCameras().ToString();
                json["supportsStereo"] = meta.SupportsStereoImageCapture();
            }

            XRCpuImage image = default;
            var acquired = false;
            while (SpikeLog.NowMs - waitStart < 15000)
            {
                if (manager.TryAcquireLatestCpuImage(out image))
                {
                    acquired = true;
                    break;
                }
                await UniTask.Yield(ct);
            }
            json["acquired"] = acquired;
            json["msToFirstImage"] = SpikeLog.NowMs - waitStart;
            if (!acquired)
            {
                SpikeLog.Result("camera", json);
                return null;
            }

            byte[] uploadJpeg;
            using (image)
            {
                json["dimensions"] = $"{image.dimensions.x}x{image.dimensions.y}";
                json["format"] = image.format.ToString();
                json["planeCount"] = image.planeCount;
                json["timestamp"] = image.timestamp;
                json["realtimeSinceStartup"] = Time.realtimeSinceStartupAsDouble;
                // Candidate clock domains for image.timestamp (UNVERIFIED which one): monotonic ns, boottime ns.
                json["boottimeNowSec"] = MonotonicSeconds();
                json["uptimeNowSec"] = UptimeSeconds();
                var planes = new JArray();
                for (var i = 0; i < image.planeCount; i++)
                {
                    var p = image.GetPlane(i);
                    planes.Add(new JObject { ["rowStride"] = p.rowStride, ["pixelStride"] = p.pixelStride, ["bytes"] = p.data.Length });
                }
                json["planes"] = planes;
                json["meanLuma"] = MeanLuma(image);
                json["rgba32Supported"] = image.FormatSupported(TextureFormat.RGBA32);

                var head = Camera.main != null ? Camera.main.transform : null;
                if (head != null)
                    json["headPoseAtAcquire"] = $"pos={head.position:F3} rot={head.rotation.eulerAngles:F1}";

                if (manager.TryGetIntrinsics(out var k))
                    json["intrinsics"] = IntrinsicsJson(k);
                if (meta != null)
                {
                    foreach (MetaOpenXRCameraSubsystem.CameraPosition pos in Enum.GetValues(typeof(MetaOpenXRCameraSubsystem.CameraPosition)))
                    {
                        if (meta.TryGetIntrinsicsForPosition(pos, out var kp))
                            json[$"intrinsics_{pos}"] = IntrinsicsJson(kp);
                    }
                }

                var full = image.dimensions;
                var scale = Mathf.Min(1f, (float)MaxUploadSide / Mathf.Max(full.x, full.y));
                var small = new Vector2Int(Mathf.RoundToInt(full.x * scale), Mathf.RoundToInt(full.y * scale));

                var t0 = SpikeLog.NowMs;
                var fullJpeg = await ConvertToJpegAsync(image, full, XRCpuImage.Transformation.None, 85, ct);
                json["convertFullMs"] = SpikeLog.NowMs - t0;
                Save("cam_full_none.jpg", fullJpeg);

                t0 = SpikeLog.NowMs;
                var smallNone = await ConvertToJpegAsync(image, small, XRCpuImage.Transformation.None, 80, ct);
                json["convertSmallMs"] = SpikeLog.NowMs - t0;
                Save("cam_640_none.jpg", smallNone);

                uploadJpeg = await ConvertToJpegAsync(image, small, XRCpuImage.Transformation.MirrorY, 80, ct);
                Save("cam_640_mirrory.jpg", uploadJpeg);
                json["uploadJpeg"] = $"{small.x}x{small.y} {uploadJpeg.Length} bytes (MirrorY)";
            }

            await LogLaterFramesAsync(manager, meta, json, ct);
            SpikeLog.Result("camera", json);
            return uploadJpeg;
        }

        public static double LastMeanLuma { get; private set; } = -1;

        // A black first frame can be auto-exposure warm-up or a dark room; later frames and the right camera tell which.
        static async UniTask LogLaterFramesAsync(ARCameraManager manager, MetaOpenXRCameraSubsystem meta, JObject json, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);
            if (manager.TryAcquireLatestCpuImage(out var later))
            {
                using (later)
                {
                    LastMeanLuma = MeanLuma(later);
                    json["meanLumaAfter2s"] = LastMeanLuma;
                    json["timestampAfter2s"] = later.timestamp;
                    json["uptimeSecAfter2s"] = UptimeSeconds();
                }
            }
            if (meta != null && meta.TryAcquireLatestCpuImageForPosition(MetaOpenXRCameraSubsystem.CameraPosition.RightEye, out var cinfo))
            {
                using var right = new XRCpuImage(meta.cpuImageApi, cinfo);
                json["rightEye"] = $"{right.dimensions.x}x{right.dimensions.y} {right.format} ts={right.timestamp} meanLuma={MeanLuma(right):F1}";
            }
        }

        static double MeanLuma(XRCpuImage image)
        {
            var y = image.GetPlane(0);
            var data = y.data;
            const int step = 8;
            long sum = 0;
            var count = 0;
            for (var row = 0; row < image.height; row += step)
            {
                var rowStart = row * y.rowStride;
                for (var col = 0; col < image.width; col += step)
                {
                    sum += data[rowStart + col * y.pixelStride];
                    count++;
                }
            }
            return count > 0 ? (double)sum / count : -1;
        }

        static async UniTask<byte[]> ConvertToJpegAsync(XRCpuImage image, Vector2Int outDims,
            XRCpuImage.Transformation transformation, int quality, CancellationToken ct)
        {
            var conversion = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, transformation)
            {
                outputDimensions = outDims,
            };
            var request = image.ConvertAsync(conversion);
            try
            {
                while (!request.status.IsDone())
                    await UniTask.Yield(ct);
                if (request.status != XRCpuImage.AsyncConversionStatus.Ready)
                    throw new InvalidOperationException($"ConvertAsync failed: {request.status}");

                var texture = new Texture2D(outDims.x, outDims.y, TextureFormat.RGBA32, false);
                try
                {
                    texture.LoadRawTextureData(request.GetData<byte>());
                    texture.Apply(false, false);
                    return texture.EncodeToJPG(quality);
                }
                finally
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        static JObject IntrinsicsJson(XRCameraIntrinsics k) => new()
        {
            ["fx"] = k.focalLength.x,
            ["fy"] = k.focalLength.y,
            ["cx"] = k.principalPoint.x,
            ["cy"] = k.principalPoint.y,
            ["w"] = k.resolution.x,
            ["h"] = k.resolution.y,
        };

        static double MonotonicSeconds()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var clock = new AndroidJavaClass("android.os.SystemClock");
            return clock.CallStatic<long>("elapsedRealtimeNanos") / 1e9;
#else
            return -1;
#endif
        }

        // SystemClock.uptimeMillis = CLOCK_MONOTONIC (stops in deep sleep); elapsedRealtimeNanos = CLOCK_BOOTTIME.
        static double UptimeSeconds()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var clock = new AndroidJavaClass("android.os.SystemClock");
            return clock.CallStatic<long>("uptimeMillis") / 1e3;
#else
            return -1;
#endif
        }

        static void Save(string name, byte[] bytes)
        {
            var path = Path.Combine(Application.persistentDataPath, name);
            File.WriteAllBytes(path, bytes);
            SpikeLog.Info($"saved {name} ({bytes.Length} bytes)");
        }
    }

    /// <summary>
    /// Camera2 characteristics for the passthrough cameras: LENS_POSE_* (extrinsics), intrinsic calibration,
    /// and the Meta vendor key com.meta.extra_metadata.position. Keys are constructed by name
    /// (CameraCharacteristics.Key(String, Class)) so platform and vendor keys share one code path.
    /// </summary>
    static class Camera2Probe
    {
        public static void Run()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var json = new JObject();
            try
            {
                using var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var cameraManager = activity.Call<AndroidJavaObject>("getSystemService", "camera");
                var ids = cameraManager.Call<string[]>("getCameraIdList");
                json["cameraIds"] = new JArray(ids);

                using var classClass = new AndroidJavaClass("java.lang.Class");
                using var floatArrayType = classClass.CallStatic<AndroidJavaObject>("forName", "[F");
                using var integerType = classClass.CallStatic<AndroidJavaObject>("forName", "java.lang.Integer");
                using var rectType = classClass.CallStatic<AndroidJavaObject>("forName", "android.graphics.Rect");
                using var sizeType = classClass.CallStatic<AndroidJavaObject>("forName", "android.util.Size");

                var cameras = new JArray();
                foreach (var id in ids)
                {
                    var cam = new JObject { ["id"] = id };
                    try
                    {
                        using var chars = cameraManager.Call<AndroidJavaObject>("getCameraCharacteristics", id);
                        cam["metaPosition"] = GetInt(chars, "com.meta.extra_metadata.position", integerType);
                        cam["lensFacing"] = GetInt(chars, "android.lens.facing", integerType);
                        cam["poseReference"] = GetInt(chars, "android.lens.poseReference", integerType);
                        cam["poseTranslation"] = GetFloats(chars, "android.lens.poseTranslation", floatArrayType);
                        cam["poseRotation"] = GetFloats(chars, "android.lens.poseRotation", floatArrayType);
                        cam["intrinsicCalibration"] = GetFloats(chars, "android.lens.intrinsicCalibration", floatArrayType);
                        cam["distortion"] = GetFloats(chars, "android.lens.distortion", floatArrayType);
                        cam["activeArraySize"] = GetString(chars, "android.sensor.info.activeArraySize", rectType);
                        cam["pixelArraySize"] = GetString(chars, "android.sensor.info.pixelArraySize", sizeType);
                        cam["keys"] = KeyNames(chars);
                    }
                    catch (Exception e)
                    {
                        cam["error"] = e.Message;
                    }
                    cameras.Add(cam);
                }
                json["cameras"] = cameras;
            }
            catch (Exception e)
            {
                json["error"] = e.ToString();
            }
            SpikeLog.Result("camera2", json);
#else
            SpikeLog.Info("camera2 probe skipped (not on Android device)");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject Get(AndroidJavaObject chars, string keyName, AndroidJavaObject type)
        {
            using var key = new AndroidJavaObject("android.hardware.camera2.CameraCharacteristics$Key", keyName, type);
            return chars.Call<AndroidJavaObject>("get", key);
        }

        static JToken GetFloats(AndroidJavaObject chars, string keyName, AndroidJavaObject type)
        {
            try
            {
                using var value = Get(chars, keyName, type);
                if (value == null)
                    return JValue.CreateNull();
                return new JArray(AndroidJNIHelper.ConvertFromJNIArray<float[]>(value.GetRawObject()));
            }
            catch (Exception e)
            {
                return $"error: {e.Message}";
            }
        }

        static JToken GetInt(AndroidJavaObject chars, string keyName, AndroidJavaObject type)
        {
            try
            {
                using var value = Get(chars, keyName, type);
                return value == null ? JValue.CreateNull() : value.Call<int>("intValue");
            }
            catch (Exception e)
            {
                return $"error: {e.Message}";
            }
        }

        static JToken GetString(AndroidJavaObject chars, string keyName, AndroidJavaObject type)
        {
            try
            {
                using var value = Get(chars, keyName, type);
                return value == null ? JValue.CreateNull() : value.Call<string>("toString");
            }
            catch (Exception e)
            {
                return $"error: {e.Message}";
            }
        }

        static JArray KeyNames(AndroidJavaObject chars)
        {
            var names = new JArray();
            using var keys = chars.Call<AndroidJavaObject>("getKeys");
            var count = keys.Call<int>("size");
            for (var i = 0; i < count; i++)
            {
                using var key = keys.Call<AndroidJavaObject>("get", i);
                names.Add(key.Call<string>("getName"));
            }
            return names;
        }
#endif
    }
}
