using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Providers.MetaApi.Vision;

namespace Jolly.Tests.EditMode.Vision.Doubles
{
    sealed class FakeMetaApiJsonClient : IMetaApiJsonClient
    {
        readonly Func<CancellationToken, UniTask<string>> _respond;

        FakeMetaApiJsonClient(Func<CancellationToken, UniTask<string>> respond) => _respond = respond;

        public int Calls { get; private set; }
        public string LastPath { get; private set; }
        public string LastBody { get; private set; }

        public static FakeMetaApiJsonClient Returning(string body) => new(_ => UniTask.FromResult(body));

        public static FakeMetaApiJsonClient Throwing(Exception e) => new(_ => UniTask.FromException<string>(e));

        /// <summary>Never answers; completes as cancelled when the request token is cancelled, like an aborted request.</summary>
        public static FakeMetaApiJsonClient Hanging() => new(ct => UniTask.Never<string>(ct));

        public UniTask<string> PostJson(string path, string jsonBody, CancellationToken ct)
        {
            Calls++;
            LastPath = path;
            LastBody = jsonBody;
            return _respond(ct);
        }
    }
}
