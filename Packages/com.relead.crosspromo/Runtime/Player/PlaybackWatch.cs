namespace Relead.CrossPromo
{
    /// <summary>
    /// Detects a video that stopped making progress (truncated / undecodable file, zero frames, decoder hang)
    /// without raising an error: true once the frame number has not changed for <c>limitSec</c> of active time.
    /// Looping back to frame 0 counts as progress. Pure, driven by the overlay.
    /// </summary>
    public sealed class StallWatch
    {
        public const float DefaultLimitSec = 5f;

        readonly float _limitSec;
        long _lastFrame = long.MinValue;
        float _stillSec;

        public StallWatch(float limitSec = DefaultLimitSec) { _limitSec = limitSec; }

        public float StillSec => _stillSec;

        public bool Tick(float dt, long frame)
        {
            if (frame != _lastFrame)
            {
                _lastFrame = frame;
                _stillSec = 0f;
                return false;
            }
            if (dt > 0) _stillSec += dt;
            return _stillSec >= _limitSec;
        }

        /// <summary>After a pause: the decoder may need a moment to restart.</summary>
        public void Reset() { _stillSec = 0f; }
    }

    /// <summary>
    /// The ad plays only while the app has focus and is not paused (notification shade, incoming call, app switch,
    /// store opened): content and the watch clock stop meanwhile, so the reward needs real watching. Pure.
    /// </summary>
    public sealed class PlaybackGate
    {
        public enum Change { None, Paused, Resumed }

        bool _focus = true;
        bool _paused;

        public bool Active => _focus && !_paused;

        public Change SetFocus(bool focus) => Set(focus, _paused);
        public Change SetPaused(bool paused) => Set(_focus, paused);

        Change Set(bool focus, bool paused)
        {
            bool was = Active;
            _focus = focus;
            _paused = paused;
            if (was == Active) return Change.None;
            return Active ? Change.Resumed : Change.Paused;
        }
    }
}
