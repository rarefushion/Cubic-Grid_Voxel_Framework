using System.Reflection;
using ImGuiNET;

public static class BlockPickerGUI
{

    public static ushort block = BlockIDs.Stone;
    public const ushort MaxBlock = BlockIDs.WaterFull;

    public static void OnRender(double delta)
    {
        int attemptBlock = block;
        ImGui.Begin("Block Picker");
        ImGui.InputInt(GetBlockName(block), ref attemptBlock);
        ImGui.End();
        if (attemptBlock < 0)
            attemptBlock = MaxBlock;
        if (attemptBlock > MaxBlock)
            attemptBlock = 0;
        if (attemptBlock == BlockIDs.WaterFull + 1)
            attemptBlock = BlockIDs.Air;
        if (attemptBlock == BlockIDs.WaterFull + BlockIDs.WaterLevels - 1)
            attemptBlock = BlockIDs.WaterFull;
        block = (ushort)attemptBlock;
    }

    public static string GetBlockName(ushort value)
    {
        // Get all public and non-public static fields (constants are static by default)
        FieldInfo[] fields = typeof(BlockIDs).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        foreach (FieldInfo field in fields)
        {
            // Verify the field is a literal (which is how C# implements 'const')
            if (field.IsLiteral && field.FieldType == typeof(ushort))
            {
                ushort fieldValue = (ushort)field.GetValue(null)!;
                if (fieldValue == value)
                {
                    return field.Name;
                }
            }
        }

        return string.Empty; // Or throw an exception if not found
    }
}
