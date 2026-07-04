using System.Numerics;
using GalensUnified.CubicGrid.Core;

namespace GalensUnified.CubicGrid.Renderer.NET.Shapes;

/// <summary>Creates a cube at a specified height.</summary>
/// <param name="cubeShapeTopFaceID">Reuse the top cube face shapeID.</param>
/// <param name="cubeShapeBottomFaceID">Reuse the bottom cube face shapeID.</param>
/// <param name="height">The height the top face will sit. between 0 and 1 or the shape exceeds it's cubic bounds.</param>
/// <remarks>Currently does not smoothly connect to other heights.</remarks>
public class LiquidShape(int cubeShapeTopFaceID, int cubeShapeBottomFaceID, float height) : IShape
{
    public readonly int[] shapeIDByDirection = [ 0, 0, cubeShapeTopFaceID, cubeShapeBottomFaceID, 0, 0 ];

    public Shape[] Create(int nextShapeID)
    {
        Shape[] toReturn =
        [
            Cube.CreateFace(Direction.Back),
            Cube.CreateFace(Direction.Front),
            Cube.CreateFace(Direction.Left),
            Cube.CreateFace(Direction.Right)
        ];
        shapeIDByDirection[(int)Direction.Back] = nextShapeID;
        shapeIDByDirection[(int)Direction.Front] = nextShapeID + 1;
        shapeIDByDirection[(int)Direction.Left] = nextShapeID + 2;
        shapeIDByDirection[(int)Direction.Right] = nextShapeID + 3;
        for (int i = 0; i < 4; i++)
            for (int v = 0; v < toReturn[i].Vertices.Length; v++)
                toReturn[i].Vertices[v].position *= new Vector3(1, height, 1);
        return toReturn;
    }

    public ShapeInstance[] Instance(Vector3 position, BlockRenderData renderData, List<Vector3> faceTints, List<Direction> facesVisible, Direction up, int forward)
    {
        List<ShapeInstance> toReturn = [];
        bool drawTop = false;
        Vector3? topTint = null; // If isn't set in faceTints assume a side faces tint, if those don't exist assume full bright.
        for (int i = 0; i < facesVisible.Count; i++)
        {
            if (facesVisible[i] != Direction.Bottom)
                drawTop = true;
            if (facesVisible[i] != Direction.Top)
            {
                toReturn.Add(new
                (
                    position,
                    renderData.GetTextureID(facesVisible[i]),
                    faceTints[i],
                    shapeIDByDirection[(int)facesVisible[i]],
                    up,
                    forward
                ));
                if (topTint == null)
                    topTint = faceTints[i];
            }
            else
                topTint = faceTints[i];
        }
        if (drawTop)
            toReturn.Add(new
            (
                position - Vector3.UnitY * (1 - height),
                renderData.GetTextureID(Direction.Top),
                topTint?? Vector3.One,
                shapeIDByDirection[(int)Direction.Top],
                up,
                forward
            ));
        return [.. toReturn];
    }
}