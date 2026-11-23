namespace IronRequiem.Editor
{
    // Executions v02: queue entry for the VisualOverhaulDriver request file (it only invokes IronRequiem.Editor.VisualOverhaul*).
    public static class VisualOverhaulExecutions
    {
        public static string ApplyPrefabs() => IronRequiem.EditorTools.ExecutionsAuthoring.Apply();
    }
}
