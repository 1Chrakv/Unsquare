namespace Unsquare.Core.Diagnostics
{
    /// <summary>Phase and counter names of one variant bake, as they appear in the log.</summary>
    public static class BakePhase
    {
        public const string Obtain = "obtain";
        public const string Read = "read";
        public const string Deform = "deform";
        public const string Pack = "pack";
        public const string Write = "write";
        public const string AssetAdd = "add";
        public const string AssetSave = "save";
        public const string AssetUnload = "unload";
        public const string RenderClone = "renderClone";
        public const string PrefabClone = "prefabClone";
        public const string Attachments = "attachments";
        public const string AddPrefab = "addPrefab";
        public const string Manifest = "manifest";

        public const string Vertices = "vertices";
        public const string Meshes = "meshes";

        /// <summary>Meshes that could not take the vertex job and were deformed on the main thread.</summary>
        public const string ManagedMeshes = "on the managed path";
    }
}
