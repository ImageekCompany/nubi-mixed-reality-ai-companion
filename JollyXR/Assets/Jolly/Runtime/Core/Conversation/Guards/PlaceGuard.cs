using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// The place phrases the model answered in words too often (ADR-0051): live on 2026-11-03, "That over there is the
    /// kitchen." got a chatty reply and no remember_place, which reads as remembered and fails the next turn, and "Where
    /// is the kitchen?" got no call in 1 run of 5. Naming a room while showing it, and asking after a place already remembered,
    /// run their tool with no model round. Rooms are a closed list, so "this is the best" stays with the model.
    /// </summary>
    public sealed class PlaceGuard
    {
        const int MaxWords = 10;

        readonly ISceneMemory _scene;
        readonly LocalizedPhrases _rooms;
        readonly LocalizedPhrases _shows;
        readonly LocalizedPhrases _asks;
        readonly LocalizedPhrases _whereItGoes;
        readonly LocalizedPhrases _negations;
        readonly List<string> _tokens = new();

        public PlaceGuard(ILocalizer localizer, ISceneMemory scene)
        {
            _scene = scene;
            _rooms = new LocalizedPhrases(localizer, GuardTables.PlaceRooms);
            _shows = new LocalizedPhrases(localizer, GuardTables.PlaceShows);
            _asks = new LocalizedPhrases(localizer, GuardTables.PlaceAsks);
            _whereItGoes = new LocalizedPhrases(localizer, GuardTables.PlaceWhereItGoes);
            _negations = new LocalizedPhrases(localizer, GuardTables.ActionNegations);
        }

        /// <summary>The tool a short place phrase asks for; <paramref name="named"/> is the place's name.</summary>
        public bool TryMatch(string transcript, out string tool, out string argumentsJson, out string named)
        {
            tool = argumentsJson = named = null;
            if (string.IsNullOrEmpty(transcript))
                return false;
            TextNormalizer.Tokenize(transcript, _tokens);
            if (_tokens.Count > MaxWords || Contains(_negations))
                return false;

            if (transcript.IndexOf('?') < 0 && Contains(_shows) && TryFindRoom(out named))
            {
                tool = ToolNames.RememberPlace;
                argumentsJson = "{\"name\":\"" + named + "\",\"where\":\"there\"}";
                return true;
            }
            if (Contains(_asks) && TryFindRemembered(out named))
            {
                tool = ToolNames.ShowPlace;
                argumentsJson = "{\"place\":\"" + named + "\"}";
                return true;
            }
            return false;
        }

        /// <summary>"Where do these go?" with a place to name; the answer needs the camera, so the model still runs it.</summary>
        public bool AsksWhereItGoes(string transcript)
        {
            if (string.IsNullOrEmpty(transcript) || _scene.Places.Count == 0)
                return false;
            TextNormalizer.Tokenize(transcript, _tokens);
            return Contains(_whereItGoes);
        }

        bool Contains(LocalizedPhrases phrases)
        {
            foreach (var phrase in phrases.Current)
            {
                if (TextNormalizer.IndexOf(_tokens, phrase) >= 0)
                    return true;
            }
            return false;
        }

        bool TryFindRoom(out string named)
        {
            foreach (var room in _rooms.Current)
            {
                if (TextNormalizer.IndexOf(_tokens, room) >= 0)
                {
                    named = string.Join(" ", room);
                    return true;
                }
            }
            named = null;
            return false;
        }

        bool TryFindRemembered(out string named)
        {
            foreach (var place in _scene.Places)
            {
                if (TextNormalizer.IndexOf(_tokens, TextNormalizer.Tokenize(place.Name)) >= 0)
                {
                    named = place.Name;
                    return true;
                }
            }
            named = null;
            return false;
        }
    }
}
