namespace Supercent.PlayableAI.Generation.Editor.Compile
{
    public static class DefaultProperties
    {
        // Camera focus timing is slightly relaxed so tutorial targets read clearly before the return phase starts.
        private const float CAMERA_MOVING_TIME = 0.75f;
        private const float CAMERA_RETURN_DELAY = 1f;

        public static float CameraMovingTime => CAMERA_MOVING_TIME;
        public static float CameraReturnDelay => CAMERA_RETURN_DELAY;
    }
}
