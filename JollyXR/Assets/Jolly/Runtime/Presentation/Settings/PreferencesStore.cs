using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>Where the settings panel keeps user preferences between sessions.</summary>
    public interface IPreferencesStore
    {
        UserPreferencesSnapshot Load();
        void Save(UserPreferencesSnapshot preferences);
    }

    /// <summary>PlayerPrefs-backed store. Saves immediately: Quest does not reliably run quit callbacks.</summary>
    public sealed class PlayerPrefsPreferencesStore : IPreferencesStore
    {
        const string Seated = "jolly.prefs.seated";
        const string CaptionSize = "jolly.prefs.caption_size";
        const string SpeechRate = "jolly.prefs.speech_rate";
        const string Volume = "jolly.prefs.volume";
        const string PttEitherHand = "jolly.prefs.ptt_either_hand";

        public UserPreferencesSnapshot Load()
        {
            var defaults = new UserPreferencesSnapshot();
            return new UserPreferencesSnapshot
            {
                Seated = PlayerPrefs.GetInt(Seated, defaults.Seated ? 1 : 0) != 0,
                CaptionSize = PlayerPrefs.GetInt(CaptionSize, defaults.CaptionSize),
                SpeechRate = PlayerPrefs.GetFloat(SpeechRate, defaults.SpeechRate),
                Volume = PlayerPrefs.GetFloat(Volume, defaults.Volume),
                PttEitherHand = PlayerPrefs.GetInt(PttEitherHand, defaults.PttEitherHand ? 1 : 0) != 0,
            };
        }

        public void Save(UserPreferencesSnapshot p)
        {
            PlayerPrefs.SetInt(Seated, p.Seated ? 1 : 0);
            PlayerPrefs.SetInt(CaptionSize, p.CaptionSize);
            PlayerPrefs.SetFloat(SpeechRate, p.SpeechRate);
            PlayerPrefs.SetFloat(Volume, p.Volume);
            PlayerPrefs.SetInt(PttEitherHand, p.PttEitherHand ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
