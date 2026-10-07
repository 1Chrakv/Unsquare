namespace Unsquare.Core.Persistence
{
    public enum JsonReadStatus
    {
        /// <summary>Neither the file nor its backup exists.</summary>
        Missing,
        Ok,

        /// <summary>The main file was missing or broken; the .bak copy was used.</summary>
        FromBackup,

        /// <summary>Files exist but none could be parsed. Callers must not treat this as "empty".</summary>
        Corrupt,
    }
}
