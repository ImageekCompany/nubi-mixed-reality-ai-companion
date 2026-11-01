using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Loads the memories at startup and keeps them on disk after every turn (architecture §8): summarise once the caps
    /// are exceeded, then save. Never throws except on cancellation of <see cref="Load"/>; a failed save or summary is
    /// logged and retried after the next turn.
    /// </summary>
    public sealed class MemoryKeeper
    {
        readonly IConversationMemory _conversation;
        readonly ISceneMemory _scene;
        readonly IChatModel _model;
        readonly IPersonaProvider _personas;
        readonly ILog _log;

        public MemoryKeeper(IConversationMemory conversation, ISceneMemory scene, IChatModel model, IPersonaProvider personas,
                            ILog log)
        {
            _conversation = conversation;
            _scene = scene;
            _model = model;
            _personas = personas;
            _log = log;
        }

        public async UniTask Load(CancellationToken ct)
        {
            await _conversation.Load(_personas.Current.Id, null, ct);
            await _scene.Load(ct);
        }

        public async UniTask AfterTurn(CancellationToken ct)
        {
            if (_conversation.NeedsSummarization)
            {
                try
                {
                    await _conversation.Summarize(_model, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ProviderException e)
                {
                    _log.Write(LogLevel.Warning, "memory", $"evt=memory.summarize_failed kind={e.Kind} status={e.HttpStatus}");
                }
                catch (Exception e)
                {
                    _log.Write(LogLevel.Error, "memory", "evt=memory.summarize_failed", e);
                }
            }
            await Save(ct);
        }

        /// <summary>Also called by the composition root on pause and focus loss.</summary>
        public async UniTask Save(CancellationToken ct)
        {
            try
            {
                await _conversation.Save(ct);
                await _scene.Save(ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Error, "memory", "evt=memory.save_failed", e);
            }
        }
    }
}
