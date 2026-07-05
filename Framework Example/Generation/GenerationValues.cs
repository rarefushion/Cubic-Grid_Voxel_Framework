using System.Numerics;
using Silk.NET.Maths;

public static class GenerationValues
{
    public const int Seed = 1337;
    public const float WorldScale = 0.01f;
    public const int MountainHeight = 50;
    public const int MinTerrainHeight = 0;

    private static readonly FastNoiseLite FNL;
    public static FastNoiseLite Temperature;
    public static readonly Vector3 LushColor = new(0.0f, 1.0f, 0.0f); // rgb(0, 255, 0)
    public static readonly Vector3 AutumnColor = new(1, 0.53f, 0.17f); // rgb(255, 136, 44)
    public static readonly Vector3 AutumnWaterColor = new(0.39f, 0.0f, 1.0f); // rgb(100, 0, 255)
    public static readonly Vector3 LushWaterColor = new(0.0f, 1.0f, 1.0f); // rgb(0, 255, 255)

    public static bool IsErodid(Vector3D<int> blockPosition)
    {
        float errosion = FNL.GetNoise(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return errosion > 0.5f;
    }

    public static int GetMountainHeight(Vector3D<int> blockPosition)
    {
        // Doesn't use Y(height) so the value is the same regardless of height.
        float mountainous = (FNL.GetNoise(blockPosition.X, blockPosition.Z) + 1) / 2;
        return (int)(mountainous * MountainHeight) + MinTerrainHeight;
    }

    public static float GetTemperature(Vector3 position) =>
        (Temperature.GetNoise(position.X, position.Z) + 1) / 2;

    static GenerationValues()
    {
        FNL = new(Seed);
        FNL.SetFrequency(WorldScale);
        Temperature = new(Seed);
        Temperature.SetFrequency(WorldScale * 0.2f);
    }
}