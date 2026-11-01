using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Jolly.XR.Permissions
{
    /// <summary>
    /// UnityEngine.Android.Permission behind IRuntimePermissions. Off-device (Editor, Link) everything counts as granted.
    /// While a system dialog is up Unity pauses the PlayerLoop, so no in-app timeout runs during the request.
    /// </summary>
    public sealed class AndroidRuntimePermissions : IRuntimePermissions
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        public bool IsGranted(string permission) => Permission.HasUserAuthorizedPermission(permission);

        public async UniTask Request(IReadOnlyList<string> permissions, CancellationToken ct)
        {
            if (permissions.Count == 0)
                return;
            var answered = new UniTaskCompletionSource();
            var pending = permissions.Count;
            void Answer(string _)
            {
                if (Interlocked.Decrement(ref pending) <= 0)
                    answered.TrySetResult();
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += Answer;
            callbacks.PermissionDenied += Answer;
            callbacks.PermissionRequestDismissed += _ => answered.TrySetResult();
            var request = new string[permissions.Count];
            for (var i = 0; i < request.Length; i++)
                request[i] = permissions[i];
            Permission.RequestUserPermissions(request, callbacks);

            // The callbacks arrive through an AndroidJavaProxy on a Java thread.
            await answered.Task.AttachExternalCancellation(ct);
            await UniTask.SwitchToMainThread(ct);
        }
#else
        public bool IsGranted(string permission) => true;

        public UniTask Request(IReadOnlyList<string> permissions, CancellationToken ct) => UniTask.CompletedTask;
#endif
    }
}
