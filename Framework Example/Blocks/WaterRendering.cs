using GalensUnified.CubicGrid.Renderer.NET;
using GalensUnified.CubicGrid.Renderer.NET.Shapes;
using static BlockIDs;

public static class WaterRendering
{
    /// <remarks>Does not include full, that uses the cube shape.</remarks>
    public static readonly LiquidShape[] shapeByWaterLevel = new LiquidShape[WaterLevels - 1];

    public const ushort WaterLowest = WaterFull + WaterLevels;
    public static ushort GetBlock(int waterLevel) => (ushort)(WaterLowest - waterLevel);
    public static int GetLevel(ushort block) => WaterLevels - (block - WaterFull);
    public static bool IsWater(ushort blockID) => blockID >= WaterFull && blockID < WaterLowest;

    public static Shape[] CreatShapes
    (
        int cubeShapeTopFaceID,
        int cubeShapeBottomFaceID,
        int waterStartShapeID
    )
    {
        List<Shape> toReturn = [];
        for (int i = 0; i < shapeByWaterLevel.Length; i++)
        {
            float waterLevel = (WaterLevels - i - 1) / (float)WaterLevels;
            shapeByWaterLevel[i] = new(cubeShapeTopFaceID, cubeShapeBottomFaceID, waterLevel);
            toReturn.AddRange(shapeByWaterLevel[i].Create(waterStartShapeID + toReturn.Count));
        }
        return [.. toReturn];
    }
}