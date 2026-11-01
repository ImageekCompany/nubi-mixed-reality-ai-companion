using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.XR.Capture;
using Jolly.XR.Permissions;
using Jolly.XR.Tracking;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.Features.Meta;
using CameraPosition = UnityEngine.XR.OpenXR.Features.Meta.MetaOpenXRCameraSubsystem.CameraPosition;

namespace Jolly.XR.MetaQuest
{
    /// <summary>
    /// Quest passthrough camera (left lens) as ICameraFrameSource (ADR-0003). One capture: acquire the latest CPU image,
    /// convert straight to the target size on the platform's worker (XRCpuImage.ConvertAsync with outputDimensions:
    /// 12–14 ms for 640² vs 75 ms at full 1728², device-spike.md §1), copy into the reused buffer, and pose it at its
    /// exposure time: head pose from the history at the image's CLOCK_MONOTONIC timestamp × the Camera2 lens pose.
    /// </summary>
    public sealed class MetaPassthroughFrameSource : ICameraFrameSource
    {
        const string Provider = "meta-passthrough";
        const double FrameTimeoutSeconds = 1.0;
        const CameraPosition Lens = CameraPosition.LeftEye;
        // fx / width of the Quest 3 left camera (1172.536 / 1728), used only if the runtime reports no intrinsics.
        const float QuestFocalRatio = 0.6786f;
        const int QuestCameraSide = 1728;

        readonly ARCameraManager _cameraManager;
        readonly IHeadPoseHistory _headHistory;
        readonly IUserPoseProvider _user;
        readonly IRuntimePermissions _permissions;
        readonly IClock _clock;
        readonly ILog _log;
        readonly XRCpuImage.Transformation _transformation;
        readonly Pose _gyroToHead;
        readonly CameraFrameSlot _slot = new();
        Pose? _lensInHead;

        /// <param name="transformation">Row order fix-up so Rgba32 is top row first. None until the R1 lit-room check
        /// says otherwise (device-spike.md §5.1).</param>
        /// <param name="gyroToHead">Offset from the Camera2 pose reference (gyroscope) to the XR head pose; identity
        /// until the R1 marker test measures it.</param>
        public MetaPassthroughFrameSource(ARCameraManager cameraManager, IHeadPoseHistory headHistory, IUserPoseProvider user,
            IRuntimePermissions permissions, IClock clock, ILog log,
            XRCpuImage.Transformation transformation = XRCpuImage.Transformation.None, Pose? gyroToHead = null)
        {
            _cameraManager = cameraManager;
            _headHistory = headHistory;
            _user = user;
            _permissions = permissions;
            _clock = clock;
            _log = log;
            _transformation = transformation;
            _gyroToHead = gyroToHead ?? Pose.identity;
        }

        public CameraAvailability Availability
        {
            get
            {
                if (!_permissions.IsGranted(XrPermissions.HeadsetCamera))
                    return CameraAvailability.NoPermission;
                if (_cameraManager == null || !_cameraManager.isActiveAndEnabled)
                    return CameraAvailability.Starting;
                var subsystem = _cameraManager.subsystem;
                if (subsystem == null)
                    return CameraAvailability.Starting;
                if (subsystem is not MetaOpenXRCameraSubsystem)
                    return CameraAvailability.NotSupported;
                return subsystem.running ? CameraAvailability.Ready : CameraAvailability.Starting;
            }
        }

        public async UniTask<CameraFrame> Capture(int maxSide, CancellationToken ct)
        {
            var availability = Availability;
            if (availability != CameraAvailability.Ready)
                throw new ProviderException(Provider, ProviderErrorKind.Protocol, "camera " + availability);
            var meta = (MetaOpenXRCameraSubsystem)_cameraManager.subsystem;

            var side = Mathf.Min(maxSide, NativeSide(meta));
            var buffer = await _slot.Acquire(side * side * 4, ct);
            try
            {
                var cinfo = await AcquireLatest(meta, ct);
                var acquiredAt = _clock.Now;
                using var image = new XRCpuImage(meta.cpuImageApi, cinfo);
                var size = FrameSize.Fit(image.dimensions, side);
                var pose = CameraPoseAt(image.timestamp);
                var intrinsics = ScaledIntrinsics(meta, image.dimensions, size);
                await ConvertInto(image, size, buffer, ct);
                return _slot.Publish(size.x, size.y, intrinsics, pose, acquiredAt);
            }
            catch
            {
                _slot.Abort();
                throw;
            }
        }

        public void Dispose() => _slot.Dispose();

        async UniTask<XRCpuImage.Cinfo> AcquireLatest(MetaOpenXRCameraSubsystem meta, CancellationToken ct)
        {
            var deadline = _clock.Now + FrameTimeoutSeconds;
            XRCpuImage.Cinfo cinfo;
            while (!meta.TryAcquireLatestCpuImageForPosition(Lens, out cinfo))
            {
                if (_clock.Now > deadline)
                    throw new ProviderException(Provider, ProviderErrorKind.Protocol, "no camera frame within 1 s");
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            return cinfo;
        }

        async UniTask ConvertInto(XRCpuImage image, Vector2Int size, NativeArray<byte> buffer, CancellationToken ct)
        {
            var conversion = image.ConvertAsync(new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, _transformation)
            {
                outputDimensions = size,
            });
            using (conversion)
            {
                while (!conversion.status.IsDone())
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (conversion.status != XRCpuImage.AsyncConversionStatus.Ready)
                    throw new ProviderException(Provider, ProviderErrorKind.Protocol, "conversion " + conversion.status);
                NativeArray<byte>.Copy(conversion.GetData<byte>(), buffer, size.x * size.y * 4);
            }
        }

        Pose CameraPoseAt(double monotonicTimestamp)
        {
            var exposure = monotonicTimestamp - MonotonicClock.OffsetFromRealtime();
            var head = !double.IsNaN(exposure) && _headHistory.TryGetHeadAt(exposure, out var past) ? past : _user.Head;
            return Compose(head, LensInHead());
        }

        // Read on the first capture, not at construction: Availability is Ready only once HEADSET_CAMERA is granted.
        Pose LensInHead()
        {
            if (_lensInHead is { } cached)
                return cached;
            if (!Camera2Extrinsics.TryReadLensPose(Camera2Extrinsics.LeftCamera, out var lensInGyro, out var error))
            {
                lensInGyro = Camera2Extrinsics.QuestLeftFallback;
                _log.Write(LogLevel.Warning, "xr", "evt=camera.extrinsics_fallback reason=\"" + error + "\"");
            }
            var lensInHead = Compose(_gyroToHead, lensInGyro);
            _lensInHead = lensInHead;
            return lensInHead;
        }

        static int NativeSide(MetaOpenXRCameraSubsystem meta) =>
            meta.TryGetIntrinsicsForPosition(Lens, out var native) && native.resolution.x > 0
                ? Mathf.Max(native.resolution.x, native.resolution.y)
                : QuestCameraSide;

        CameraIntrinsics ScaledIntrinsics(MetaOpenXRCameraSubsystem meta, Vector2Int full, Vector2Int size)
        {
            var k = meta.TryGetIntrinsicsForPosition(Lens, out var native)
                ? new CameraIntrinsics(native.focalLength, native.principalPoint, native.resolution)
                : new CameraIntrinsics(new Vector2(full.x * QuestFocalRatio, full.x * QuestFocalRatio), new Vector2(full.x * 0.5f, full.y * 0.5f), full);
            var pp = k.PrincipalPoint;
            if ((_transformation & XRCpuImage.Transformation.MirrorX) != 0)
                pp.x = k.Resolution.x - pp.x;
            if ((_transformation & XRCpuImage.Transformation.MirrorY) != 0)
                pp.y = k.Resolution.y - pp.y;
            return new CameraIntrinsics(k.FocalLength, pp, k.Resolution).Scaled((float)size.x / k.Resolution.x);
        }

        static Pose Compose(Pose parent, Pose child) =>
            new(parent.position + parent.rotation * child.position, parent.rotation * child.rotation);
    }
}
