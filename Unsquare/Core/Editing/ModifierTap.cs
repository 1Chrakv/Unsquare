using System;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Detects a tap of a modifier key: pressed and released on its own within a short time, with no other key or
    /// mouse input meanwhile. Shortcuts that use the modifier (Ctrl+Z) therefore never count as a tap.
    /// </summary>
    public sealed class ModifierTap
    {
        public const float DefaultMaxSeconds = 0.5f;

        private readonly float _maxSeconds;
        private bool _held;
        private bool _spoiled;
        private float _pressTime;

        public ModifierTap(float maxSeconds = DefaultMaxSeconds)
        {
            if (!(maxSeconds > 0f))
                throw new ArgumentException("The tap time must be positive.", nameof(maxSeconds));
            _maxSeconds = maxSeconds;
        }

        /// <summary>Feeds one frame; true on the release that completes a tap.</summary>
        /// <param name="held">The modifier is down.</param>
        /// <param name="otherInput">Any other key or mouse button is down (or the wheel turned) this frame.</param>
        /// <param name="time">Seconds of an unscaled clock.</param>
        public bool Update(bool held, bool otherInput, float time)
        {
            if (held)
            {
                if (!_held)
                {
                    _held = true;
                    _spoiled = false;
                    _pressTime = time;
                }
                _spoiled |= otherInput;
                return false;
            }
            if (!_held)
                return false;
            _held = false;
            return !_spoiled && !otherInput && time - _pressTime <= _maxSeconds;
        }
    }
}
