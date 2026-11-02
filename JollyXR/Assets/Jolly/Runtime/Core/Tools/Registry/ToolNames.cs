using System.Collections.Generic;

namespace Jolly.Core.Tools
{
    /// <summary>The tool catalogue of contracts §4.2.</summary>
    public static class ToolNames
    {
        public const string WalkTo = "walk_to";
        public const string FollowUser = "follow_user";
        public const string ComeHere = "come_here";
        public const string LookAt = "look_at";
        public const string CaptureAndIdentify = "capture_and_identify";
        public const string FindObject = "find_object";
        public const string HighlightPart = "highlight_part";
        public const string RememberPlace = "remember_place";
        public const string Stop = "stop";
        public const string PlayGesture = "play_gesture";
        public const string SetPersona = "set_persona";
        public const string GoToSurface = "go_to_surface";
        public const string RequestRoomScan = "request_room_scan";
        public const string SetHomeLocation = "set_home_location";
        /// <summary>Not a registered tool: the provider's built-in web search (ADR-0048). A persona's AllowedTools lists
        /// it like a tool name to allow it.</summary>
        public const string WebSearch = "web_search";

        public static bool IsWithheldWhenSeated(string name) => name == WalkTo || name == FollowUser || name == GoToSurface;

        /// <summary>A persona's null AllowedTools allows every tool.</summary>
        public static bool IsAllowed(IReadOnlyList<string> allowed, string name)
        {
            if (allowed == null)
                return true;
            foreach (var a in allowed)
            {
                if (a == name)
                    return true;
            }
            return false;
        }
    }
}
