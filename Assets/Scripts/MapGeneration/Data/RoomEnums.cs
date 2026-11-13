using UnityEngine;

namespace LightSouls.MapGeneration
{
    /// <summary>
    /// Semantic role of a room module within a generated dungeon level.
    /// </summary>
    public enum RoomType
    {
        Start = 0,
        Normal = 1,
        Combat = 2,
        Treasure = 3,
        Corridor = 4,
        Exit = 5
    }

    /// <summary>
    /// Cardinal outward direction of a door socket on the XZ horizontal plane.
    /// </summary>
    public enum DoorDirection
    {
        North = 0, // +Z
        East = 1,  // +X
        South = 2, // -Z
        West = 3   // -X
    }

    /// <summary>
    /// Utility helpers for cardinal door directions and 90-degree Y-axis snapping math.
    /// </summary>
    public static class DoorDirectionUtility
    {
        public static Vector3 ToVector3(this DoorDirection direction)
        {
            return direction switch
            {
                DoorDirection.North => Vector3.forward,
                DoorDirection.East => Vector3.right,
                DoorDirection.South => Vector3.back,
                DoorDirection.West => Vector3.left,
                _ => Vector3.forward
            };
        }

        public static DoorDirection Opposite(this DoorDirection direction)
        {
            return (DoorDirection)(((int)direction + 2) & 3);
        }

        public static DoorDirection RotateClockwiseSteps(this DoorDirection direction, int yawSteps)
        {
            int normalizedSteps = ((yawSteps % 4) + 4) % 4;
            return (DoorDirection)(((int)direction + normalizedSteps) & 3);
        }

        public static int StepsToAlign(DoorDirection from, DoorDirection to)
        {
            return ((int)to - (int)from + 4) & 3;
        }

        public static float StepsToYawDegrees(int yawSteps)
        {
            int normalized = ((yawSteps % 4) + 4) % 4;
            return normalized * 90f;
        }

        public static DoorDirection FromHorizontalVector(Vector3 direction)
        {
            Vector2 flat = new Vector2(direction.x, direction.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                return DoorDirection.North;
            }

            if (Mathf.Abs(flat.x) > Mathf.Abs(flat.y))
            {
                return flat.x >= 0f ? DoorDirection.East : DoorDirection.West;
            }

            return flat.y >= 0f ? DoorDirection.North : DoorDirection.South;
        }
    }
}
