using UnityEngine;

namespace Relead.CrossPromo
{
    public enum StageMode
    {
        /// <summary>Media matches the host: full screen, upright.</summary>
        Upright,
        /// <summary>Waiting for the player to turn the device (animated phone icon).</summary>
        RotateHint,
        /// <summary>Device turned: stage drawn rotated ±90° inside the locked host screen.</summary>
        Rotated,
        /// <summary>Hint timed out: play in the host orientation, letterboxed.</summary>
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

        /// <summary>One step of the hint state machine. Once Rotated or Letterbox, the mode sticks (no flapping).</summary>
        public static StageMode Next(StageMode current, PromoOrientation host, PromoOrientation media,
            DeviceOrientation device, float hintElapsedSec, float hintTimeoutSec)
        {
            if (host == media) return StageMode.Upright;
            if (current == StageMode.Rotated || current == StageMode.Letterbox) return current;
            if (DeviceMatches(device, media)) return StageMode.Rotated;
            if (hintElapsedSec >= hintTimeoutSec) return StageMode.Letterbox;
            return StageMode.RotateHint;
        }
    }

    /// <summary>Skip/complete timing shared by every player. Pure, driven by unscaled delta time.</summary>
    public sealed class WatchClock
    {
        readonly float _skipAfterSec;
        public float WatchedSec { get; private set; }

        public WatchClock(float skipAfterSec) { _skipAfterSec = Mathf.Max(0, skipAfterSec); }

        public void Tick(float dt) { if (dt > 0) WatchedSec += dt; }
        public bool CanSkip => WatchedSec >= _skipAfterSec;
        /// <summary>Whole seconds left until the close button appears (for the countdown label).</summary>
        public int SecondsUntilSkip => CanSkip ? 0 : Mathf.CeilToInt(_skipAfterSec - WatchedSec);
    }
}
