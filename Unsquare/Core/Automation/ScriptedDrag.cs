using System;
using Unity.Mathematics;
using Unsquare.Core.Editing;

namespace Unsquare.Core.Automation
{
    /// <summary>
    /// A drag played over several frames like a mouse would: the total motion grows linearly from the press to the
    /// full motion at the last frame, so the session sees every intermediate state. Optionally cancelled at a frame.
    /// </summary>
    public sealed class ScriptedDrag
    {
        public ScriptedDrag(HandleMotion total, int frames, int cancelAtFrame = -1)
        {
            if (frames < 1)
                throw new ArgumentException("A drag needs at least one frame.", nameof(frames));
            if (cancelAtFrame > frames)
                throw new ArgumentException("The cancel frame lies after the last frame.", nameof(cancelAtFrame));
            Total = total;
            Frames = frames;
            CancelAtFrame = cancelAtFrame;
        }

        public HandleMotion Total { get; }
        public int Frames { get; }

        /// <summary>Frame (1-based) at which the drag is cancelled instead of moved, or -1 to release normally.</summary>
        public int CancelAtFrame { get; }

        public bool CancelsAt(int frame) => CancelAtFrame > 0 && frame >= CancelAtFrame;

        /// <summary>The total motion since the press after <paramref name="frame"/> (1..<see cref="Frames"/>).</summary>
        public HandleMotion MotionAt(int frame)
        {
            if (frame < 1 || frame > Frames)
                throw new ArgumentOutOfRangeException(nameof(frame), frame, $"Frames run from 1 to {Frames}.");
            var t = frame / (float)Frames;
            return Total.IsRotation
                ? HandleMotion.Rotate(Total.Axis, Total.Angle * t)
                : HandleMotion.Translate(Total.Translation * t);
        }

        public static ScriptedDrag Translate(float3 delta, int frames, int cancelAtFrame = -1) =>
            new(HandleMotion.Translate(delta), frames, cancelAtFrame);

        public static ScriptedDrag Rotate(int axis, float degrees, int frames, int cancelAtFrame = -1) =>
            new(HandleMotion.Rotate(axis, math.radians(degrees)), frames, cancelAtFrame);
    }
}
