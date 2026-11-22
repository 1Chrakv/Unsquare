using Colossal.Serialization.Entities;
using Unity.Collections;
using Unity.Entities;

namespace Unsquare.Integration.Saves
{
    /// <summary>
    /// The deformation of a building, saved on the building while it is on its base prefab (see
    /// <see cref="Unsquare.Core.Persistence.SavedCageCodec"/>). Type and namespace are part of the save format.
    /// </summary>
    /// <remarks>Saves store the assembly-qualified name; the version-free alias still resolves after a version bump.</remarks>
    [FormerlySerializedAs("Unsquare.Integration.Saves.SavedCage, Unsquare")]
    public struct SavedCage : IComponentData, ISerializable
    {
        /// <summary>Codec format of <see cref="Values"/>; 0 marks data that could not be read.</summary>
        public int Format;

        /// <summary>Codec values; saved as ints, held as shorts to keep the component small (cage steps fit).</summary>
        public FixedList128Bytes<short> Values;

        // Runs in Burst jobs: no managed code.
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(Format);
            writer.Write(Values.Length);
            for (int i = 0; i < Values.Length; i++)
                writer.Write((int)Values[i]); // ints in the save, whatever the memory layout
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out Format);
            reader.Read(out int count);
            Values.Clear();
            bool fits = count >= 0 && count <= Values.Capacity;
            for (int i = 0; i < count; i++)
            {
                reader.Read(out int value);
                if (value < short.MinValue || value > short.MaxValue)
                    fits = false;
                else if (Values.Length < Values.Capacity)
                    Values.Add((short)value);
            }
            if (!fits)
                Format = 0;
        }
    }
}
