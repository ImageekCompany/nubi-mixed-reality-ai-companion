using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Speech
{
    /// <summary>
    /// The canned lines (requirement 23) with their English source text. <c>CannedLineBaker</c> bakes each into
    /// <c>Content/Audio/Lines/en/{key}.wav</c>; the keys are the <c>Lines</c> and <c>LineClips</c> table keys.
    /// </summary>
    public static class CannedLineCatalog
    {
        public static readonly IReadOnlyList<CannedLineEntry> English = new[]
        {
            new CannedLineEntry("greeting_01", LineCategory.Greeting, "Hi! I'm Nubi. It's lovely to see you."),
            new CannedLineEntry("greeting_02", LineCategory.Greeting, "Hello again! How are you today?"),
            new CannedLineEntry("greeting_03", LineCategory.Greeting, "Oh, hello! I was hoping you'd stop by."),
            new CannedLineEntry("greeting_04", LineCategory.Greeting, "Good to see you! Pinch your fingers when you want to talk."),

            new CannedLineEntry("whoami_01", LineCategory.WhoAmI, "I'm Nubi, your little helper. Show me something and I'll tell you what it is."),
            new CannedLineEntry("whoami_02", LineCategory.WhoAmI, "I'm Nubi! I can chat, find your things, and keep you company."),
            new CannedLineEntry("whoami_03", LineCategory.WhoAmI, "My name is Nubi. Hold a pinch, ask me anything, then let go."),

            new CannedLineEntry("ack_01", LineCategory.Acknowledge, "Hmm!"),
            new CannedLineEntry("ack_02", LineCategory.Acknowledge, "Let me see."),
            new CannedLineEntry("ack_03", LineCategory.Acknowledge, "Okay!"),
            new CannedLineEntry("ack_04", LineCategory.Acknowledge, "One moment."),
            new CannedLineEntry("ack_05", LineCategory.Acknowledge, "Good question."),

            new CannedLineEntry("offline_01", LineCategory.Offline, "I can't reach the internet right now, but I'm still here with you."),
            new CannedLineEntry("offline_02", LineCategory.Offline, "My connection is down, so I can't think very well. Let's try again in a bit."),
            new CannedLineEntry("offline_03", LineCategory.Offline, "Sorry, I'm offline at the moment. Please check the Wi-Fi."),
            new CannedLineEntry("offline_04", LineCategory.Offline, "I lost my connection. I'll keep you company until it's back."),

            new CannedLineEntry("error_01", LineCategory.Error, "Oops, something went wrong. Could you say that again?"),
            new CannedLineEntry("error_02", LineCategory.Error, "Sorry, I got a bit muddled there. Let's try once more."),
            new CannedLineEntry("error_03", LineCategory.Error, "Hmm, that didn't work. Please ask me again."),

            new CannedLineEntry("budget_01", LineCategory.BudgetReached, "I've used up my thinking time for now, so I can only say a few simple things."),
            new CannedLineEntry("budget_02", LineCategory.BudgetReached, "My thinking allowance has run out. I'm still happy to keep you company."),

            new CannedLineEntry("idle_01", LineCategory.Idle, "Hi there!"),
            new CannedLineEntry("idle_02", LineCategory.Idle, "Hee hee, that tickles!"),
            new CannedLineEntry("idle_03", LineCategory.Idle, "Hello! Need anything?"),
            new CannedLineEntry("idle_04", LineCategory.Idle, "I'm right here if you want to chat."),
            new CannedLineEntry("idle_05", LineCategory.Idle, "Ooh, a wave! Hello to you too."),

            new CannedLineEntry("safety_01", LineCategory.SafetyNotice, "Just so you know, I'm an AI companion, not a doctor or a therapist. For anything serious, please talk to a professional."),

            new CannedLineEntry("crisis_01", LineCategory.CrisisHandoff, "I'm really glad you told me. This sounds serious, and you deserve help from a real person right now. Please call your local emergency number or a crisis line."),
            new CannedLineEntry("crisis_02", LineCategory.CrisisHandoff, "You matter, and you don't have to face this alone. Please reach out to someone you trust, or call your local emergency number now."),

            new CannedLineEntry("unknown_01", LineCategory.Unknown, "Sorry, I didn't catch that. Could you say it again?"),
            new CannedLineEntry("unknown_02", LineCategory.Unknown, "Hmm, I didn't quite hear you. Try holding the pinch while you talk."),
        };
    }
}
