using System.Numerics;
using System.Reflection;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using ImGuiNET;
using Silk.NET.Maths;

public static class BlockPickerGUI
{

    public static ushort block = BlockIDs.Stone;
    public static bool customTint = false;
    public static bool mixNaturalTint = false;
    public static float mixAmount = 0.5f;
    public static Vector3 blockTint = Vector3.One;
    public const ushort MaxBlock = BlockIDs.Blank;

    public static void OnRender(double delta)
    {
        int attemptBlock = block;
        ImGui.Begin("Block Picker");
        ImGui.InputInt(GetBlockName(block), ref attemptBlock);
        ImGui.Checkbox("Use Custom Tint", ref customTint);
        if (customTint)
        {
            ImGui.Checkbox("Mix Natural Tint", ref mixNaturalTint);
            if (mixNaturalTint)
                ImGui.SliderFloat("Mix Amount", ref mixAmount, 0, 1);
            ImGui.ColorPicker3("Block Color", ref blockTint);
        }
        ImGui.End();
        if (attemptBlock < 0)
            attemptBlock = MaxBlock;
        if (attemptBlock > MaxBlock)
            attemptBlock = 0;
        if (attemptBlock == BlockIDs.WaterFull + 1)
            attemptBlock = BlockIDs.Blank;
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

    public static ChunkCluster<TChunkDims>.IBlockData? GetBlockData<TChunkDims>(Vector3D<int> blockPosition, ushort block)
    where TChunkDims : IChunkDims
    {
        ChunkCluster<TChunkDims>.IBlockData? toReturn = ChunkProcessor<TChunkDims>.CreateBlockData(blockPosition, block);
        if (!customTint)
            return toReturn;
        if (toReturn is TintBlockData<TChunkDims> tintData)
            tintData.tint = mixNaturalTint ? MixColors(blockTint, tintData.tint) : blockTint;
        if (toReturn is WaterBlockData<TChunkDims> waterData)
            waterData.tint = mixNaturalTint ? MixColors(blockTint, waterData.tint) : blockTint;
        toReturn ??= new TintBlockData<TChunkDims>
        (
            mixNaturalTint
            ? MixColors(blockTint, GenerationValues.GetBiomeTint((Vector3)blockPosition))
            : blockTint
        );
        return toReturn;
    }

    public static Vector3 MixColors(Vector3 a, Vector3 b) =>
        Vector3.Lerp(a, b, mixAmount);
}
