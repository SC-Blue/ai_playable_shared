namespace Supercent.PlayableAI.Common.Contracts
{
    public static class FlowActionKinds
    {
        public const string CAMERA_FOCUS = "camera_focus";
        public const string ARROW_GUIDE = "arrow_guide";
        public const string REVEAL = "reveal";

        public static string[] GetAll()
        {
            return new[]
            {
                CAMERA_FOCUS,
                ARROW_GUIDE,
                REVEAL,
            };
        }
    }

    public static class FlowActionTriggerModes
    {
        public const string ON_BEAT_ENTER = "on_beat_enter";
        public const string ON_BEAT_COMPLETE = "on_beat_complete";
        public const string REACTIVE = "reactive";

        public static string[] GetAll()
        {
            return new[]
            {
                ON_BEAT_ENTER,
                ON_BEAT_COMPLETE,
                REACTIVE,
            };
        }
    }
}
