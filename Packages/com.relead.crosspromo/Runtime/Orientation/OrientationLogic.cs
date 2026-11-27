using UnityEngine;

namespace Relead.CrossPromo
{
    public enum StageMode
    {
        /// <summary>Media matches the host: full screen, upright.</summary>
        Upright,
        /// <summary>Waiting for the player to turn the device: content already plays letterboxed, small animated phone icon in the bar.</summary>
        RotateHint,
        /// <summary>Device turned: stage drawn rotated ±90° inside the locked host screen.</summary>
        Rotated,
        /// <summary>Hint timed out: keeps playing letterboxed in the host orientation; still switches to Rotated when the device turns.</summary>
        Letterbox,
    }

    /// <summary>
    /// Pure orientation rules. The package never touches Screen.orientation: the host stays locked and
    /// the promo stage is rotated inside it.
    /// </summary>
    public static class OrientationLogic
    {
        public static PromoOrientation FromSize(int width, int height)
            => width > height ? PromoOrientation.Landscape : PromoOrientation.Portrait;

        /// <summary>Unknown / FaceUp / FaceDown never match, so a phone lying flat leads to the letterbox timeout.</summary>
        public static bool DeviceMatches(DeviceOrientation d, PromoOrientation media)
        {
            switch (d)
            {
                case DeviceOrientation.Portrait:
                case DeviceOrientation.PortraitUpsideDown:
                    return media == PromoOrientation.Portrait;
                case DeviceOrientation.LandscapeLeft:
                case DeviceOrientation.LandscapeRight:
                    return media == PromoOrientation.Landscape;
                default:
                    return false;
            }
        }

        /// <summary>Counter-clockwise device rotation from natural portrait, in degrees. Null for unknown/flat.</summary>
        public static int? DeviceAngle(DeviceOrientation d)
        {
            switch (d)
            {
                case DeviceOrientation.Portrait: return 0;
                case DeviceOrientation.LandscapeLeft: return 90;
                case DeviceOrientation.PortraitUpsideDown: return 180;
                case DeviceOrientation.LandscapeRight: return 270;
                default: return null;
            }
        }

        /// <summary>Same convention for the (locked) screen orientation. AutoRotation/unknown falls back to aspect.</summary>
        public static int ScreenAngle(ScreenOrientation s, PromoOrientation hostShape)
        {
            switch (s)
            {
                case ScreenOrientation.Portrait: return 0;
                case ScreenOrientation.LandscapeLeft: return 90;
                case ScreenOrientation.PortraitUpsideDown: return 180;
                case ScreenOrientation.LandscapeRight: return 270;
                default: return hostShape == PromoOrientation.Landscape ? 90 : 0;
            }
        }

        /// <summary>
        /// uGUI z rotation (counter-clockwise positive) that makes the stage upright for the player:
        /// screenAngle − deviceAngle, normalised to (−180, 180].
        /// Portrait host + LandscapeLeft device → −90; LandscapeLeft host + Portrait device → +90.
        /// </summary>
        public static float StageRotation(int screenAngle, int deviceAngle)
        {
            int z = ((screenAngle - deviceAngle) % 360 + 360) % 360;
            if (z > 180) z -= 360;
            return z;
        }

        /// <summary>
        /// Thickness of one letterbox bar when 16:9 media is fitted into a width×height screen: top/bottom bars for
        /// landscape media, left/right bars for portrait media. 0 when the media fills that axis.
        /// </summary>
        public static float LetterboxBar(float width, float height, PromoOrientation media)
        {
            if (width <= 0 || height <= 0) return 0;
            return media == PromoOrientation.Landscape
                ? Mathf.Max(0, (height - Mathf.Min(height, width * 9f / 16f)) / 2f)
                : Mathf.Max(0, (width - Mathf.Min(width, height * 9f / 16f)) / 2f);
        }

        /// <summary>How long the device must match the media before a playing video switches to the rotated stage.</summary>
        public const float MatchStableSec = 0.5f;

        /// <summary>
        /// One step of the stage state machine (content plays in every mode, decision 0009). RotateHint and Letterbox
        /// both play letterboxed; the hint shows until hintTimeoutSec. A device match held for MatchStableSec switches
        /// to Rotated (already matching at the very start: at once). Rotated sticks (no flapping when tilting back).
        /// Flat/unknown never matches.
        /// </summary>
        public static StageMode Next(StageMode current, PromoOrientation host, PromoOrientation media,
            DeviceOrientation device, float hintElapsedSec, float hintTimeoutSec, float matchHeldSec)
        {
            if (host == media) return StageMode.Upright;
            if (current == StageMode.Rotated) return current;
            if (DeviceMatches(device, media))
            {
                bool atStart = current == StageMode.RotateHint && hintElapsedSec <= 0f;
                if (atStart || matchHeldSec >= MatchStableSec) return StageMode.Rotated;
            }
            if (current == StageMode.Letterbox || hintElapsedSec >= hintTimeoutSec) return StageMode.Letterbox;
            return StageMode.RotateHint;
        }
    }

    /// <summary>
    /// Ad timeline shared by every player (catalog rules): Skip from skipAfterSec, download panel from
    /// endCardAtSec, reward ("Earned! Close") from rewardAfterSec. Pure, driven by unscaled delta time.
    /// </summary>
    public sealed class WatchClock
    {
        readonly float _skipAfterSec;
        readonly float _endCardAtSec;
        readonly float _rewardAfterSec;
        public float WatchedSec { get; private set; }

        public WatchClock(float skipAfterSec, float endCardAtSec, float rewardAfterSec)
        {
            _skipAfterSec = Mathf.Max(0, skipAfterSec);
            _endCardAtSec = Mathf.Max(0, endCardAtSec);
            _rewardAfterSec = Mathf.Max(_endCardAtSec, rewardAfterSec);
        }

        public void Tick(float dt) { if (dt > 0) WatchedSec += dt; }
        /// <summary>Top-left Skip button visible.</summary>
        public bool CanSkip => WatchedSec >= _skipAfterSec;
        /// <summary>Download panel over the playing video.</summary>
        public bool ShowEndCard => WatchedSec >= _endCardAtSec;
        /// <summary>Watched long enough: any close now = Completed.</summary>
        public bool Earned => WatchedSec >= _rewardAfterSec;
        /// <summary>Whole seconds left until the reward (top-right countdown).</summary>
        public int SecondsUntilReward => Earned ? 0 : Mathf.CeilToInt(_rewardAfterSec - WatchedSec);
        /// <summary>0..1 towards the reward, for the progress bar.</summary>
        public float RewardProgress => _rewardAfterSec <= 0 ? 1f : Mathf.Clamp01(WatchedSec / _rewardAfterSec);
    }
}
