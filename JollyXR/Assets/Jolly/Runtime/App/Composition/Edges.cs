using System;
using Jolly.App.Platform;
using Jolly.Audio.Capture;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Net.WebSockets;
using Jolly.Providers.MetaApi.Health;
using UnityEngine;

namespace Jolly.App.Composition
{
    /// <summary>
    /// Where the app meets the outside world: files, secrets, the network, the microphone, push-to-talk, the camera and the
    /// speaker. The player uses the real ones (<see cref="ForPlayer"/>); the device self-test and the E2E tests substitute
    /// some of them and keep everything between the edges real.
    /// </summary>
    public sealed class Edges
    {
        public AppPaths Paths { get; set; }
        public ISecretsProvider Secrets { get; set; }
        public Func<bool> IsNetworkReachable { get; set; }
        public IHttpTransport Http { get; set; }
        /// <summary>Takes the provider id for errors (MuseAsrErrors.Provider).</summary>
        public Func<string, IWebSocketConnection> WebSockets { get; set; }
        public IHttpStatusClient HttpStatus { get; set; }
        /// <summary>Null uses Wit (its HTTP client is internal to the adapter).</summary>
        public ITextToSpeech TextToSpeech { get; set; }
        public IMicrophoneDevice Microphone { get; set; }
        /// <summary>Null uses the scene's pinch and controller push-to-talk.</summary>
        public IVoiceActivation Voice { get; set; }
        /// <summary>Null uses the scene's streaming output on Nubi.</summary>
        public ISpeechOutput SpeechOutput { get; set; }
        public Func<ICameraFrameSource, ICameraFrameSource> WrapCamera { get; set; } = camera => camera;
        public Func<IMaskPresenter, IMaskPresenter> WrapMasks { get; set; } = masks => masks;
        /// <summary>Whether Nubi may speak the one unprompted Space Setup offer (ADR-0025). Off unless a person is there
        /// to hear it: scripted runs would have it race their turns.</summary>
        public bool OfferRoomScan { get; set; }

        public static Edges ForPlayer()
        {
            var paths = AppPaths.ForPlayer();
            var http = new HttpTransport();
            return new Edges
            {
                Paths = paths,
                Secrets = SecretsFactory.Create(paths),
                IsNetworkReachable = () => Application.internetReachability != NetworkReachability.NotReachable,
                Http = http,
                WebSockets = provider => new ClientWebSocketConnection(provider),
                HttpStatus = new TransportStatusClient(http),
                Microphone = new UnityMicrophone(),
                OfferRoomScan = true,
            };
        }
    }
}
