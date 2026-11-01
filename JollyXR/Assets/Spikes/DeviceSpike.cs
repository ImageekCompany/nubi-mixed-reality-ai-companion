using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.XR.ARFoundation;

namespace Jolly.Spikes
{
    /// <summary>
    /// Headless on-device spike (critic.md R1, R2, R4 transport, R5 mic). Runs once on start, no user input.
    /// Paid Meta calls (vision + SAM) only run when persistentDataPath/run_paid exists; the file is consumed.
    /// </summary>
    public sealed class DeviceSpike : MonoBehaviour
    {
        const string HeadsetCamera = "horizonos.permission.HEADSET_CAMERA";
        const string UseScene = "com.oculus.permission.USE_SCENE";
        const string TtsLine = "Hi, I am Nubi. Show me something and I will tell you what it is.";
        const double DarkLumaThreshold = 12.0;

        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] AudioSource audioSource;
        [SerializeField] float startDelaySeconds = 2f;

        void Start()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            RunAsync(destroyCancellationToken).Forget();
        }

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                SpikeLog.Result("env", new JObject
                {
                    ["device"] = SystemInfo.deviceModel,
                    ["os"] = SystemInfo.operatingSystem,
                    ["unity"] = Application.unityVersion,
                    ["graphics"] = SystemInfo.graphicsDeviceType.ToString(),
                    ["scriptingBackend"] = IsIl2Cpp() ? "IL2CPP" : "Mono",
                    ["persistentDataPath"] = Application.persistentDataPath,
                });
                await UniTask.Delay(TimeSpan.FromSeconds(startDelaySeconds), cancellationToken: ct);

                var secrets = SpikeSecrets.Load();
                await RequestPermissionsAsync(ct);

                if (Permission.HasUserAuthorizedPermission(HeadsetCamera))
                {
                    // The meta-openxr camera subsystem checks HEADSET_CAMERA only in Start(); enable after the grant.
                    cameraManager.enabled = true;
                }
                else
                {
                    SpikeLog.Warn("HEADSET_CAMERA not granted: camera stays disabled (no passthrough)");
                }

                MicProbe.Result mic = null;
                if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
                    mic = await Step("mic", () => MicProbe.RunAsync(3f, ct));

                byte[] jpeg = null;
                if (cameraManager.enabled)
                {
                    jpeg = await Step("camera", () => CameraProbe.RunAsync(cameraManager, ct));
                    Camera2Probe.Run();
                }

                var ttsPcm = await Step("witTts", () => WitTtsProbe.RunAsync(secrets.WitClientToken, TtsLine, audioSource, ct));

                if (ttsPcm != null)
                {
                    await Step("asrWs", async () =>
                    {
                        await AsrWebSocketProbe.RunAsync(secrets.MetaApiKey, ttsPcm, 24000, "wit-tts", ct);
                        return true;
                    });
                }
                if (mic != null && mic.Samples.Length > 0)
                {
                    var pcm = Wav.FloatToPcm16(Wav.Resample(mic.Samples, mic.SampleRate, 24000));
                    await Step("asrWs", async () =>
                    {
                        await AsrWebSocketProbe.RunAsync(secrets.MetaApiKey, pcm, 24000, "mic", ct);
                        return true;
                    });
                }

                var paidFlag = Path.Combine(Application.persistentDataPath, "run_paid");
                if (File.Exists(paidFlag))
                {
                    File.Delete(paidFlag);
                    var testImage = Path.Combine(Application.persistentDataPath, "test_image.jpg");
                    var label = "camera";
                    if ((jpeg == null || CameraProbe.LastMeanLuma < DarkLumaThreshold) && File.Exists(testImage))
                    {
                        // Dark room / covered cameras: still measure the on-device API path with a known image.
                        jpeg = File.ReadAllBytes(testImage);
                        label = "test_image.jpg";
                    }
                    SpikeLog.Info($"vision input: {label} ({jpeg?.Length ?? 0} bytes)");
                    await Step("vision", async () =>
                    {
                        await MetaVisionProbe.RunAsync(secrets.MetaApiKey, jpeg, ct);
                        return true;
                    });
                }
                else
                {
                    SpikeLog.Info("paid calls skipped (no run_paid flag)");
                }
            }
            catch (OperationCanceledException)
            {
                SpikeLog.Warn("cancelled");
            }
            catch (Exception e)
            {
                SpikeLog.Error(e.ToString());
            }
            finally
            {
                SpikeLog.Save();
                SpikeLog.Info("DONE");
            }
        }

        static async UniTask<T> Step<T>(string name, Func<UniTask<T>> action)
        {
            SpikeLog.Info($"--- {name} ---");
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                SpikeLog.Error($"{name} failed: {e}");
                SpikeLog.Result(name + "_exception", e.GetType().Name + ": " + e.Message);
                return default;
            }
        }

        static async UniTask RequestPermissionsAsync(CancellationToken ct)
        {
            var wanted = new[] { HeadsetCamera, Permission.Microphone, UseScene };
            var missing = wanted.Where(p => !Permission.HasUserAuthorizedPermission(p)).ToList();
            var json = new JObject { ["initiallyMissing"] = new JArray(missing) };
            if (missing.Count > 0)
            {
                var pending = missing.ToHashSet();
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += p => pending.Remove(p);
                callbacks.PermissionDenied += p => pending.Remove(p);
                var start = SpikeLog.NowMs;
                Permission.RequestUserPermissions(missing.ToArray(), callbacks);
                var timedOut = await UniTask.WaitUntil(() => pending.Count == 0, cancellationToken: ct)
                    .TimeoutWithoutException(TimeSpan.FromSeconds(60));
                json["requestTimedOut"] = timedOut;
                json["requestMs"] = SpikeLog.NowMs - start;
            }
            foreach (var p in wanted)
                json[p] = Permission.HasUserAuthorizedPermission(p);
            SpikeLog.Result("permissions", json);
        }

        static bool IsIl2Cpp()
        {
#if ENABLE_IL2CPP
            return true;
#else
            return false;
#endif
        }
    }
}
