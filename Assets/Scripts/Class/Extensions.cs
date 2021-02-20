using NostalgiaOrbitDLL;
using System;
using UnityEngine;

public static class Extensions
{
    public static Vector2 ToVector(this PositionVector positionVector)
    {
        return new Vector2(positionVector.Position_X, positionVector.Position_Y);
    }
    public static PositionVector ToPositionVector(this Vector2 vector)
    {
        return new PositionVector(vector.x, vector.y);
    }

    public static float Distance(AbstractMapObject a, AbstractMapObject b)
    {
        return Distance(a.Position, b.Position);
    }
    public static float Distance(Vector2 a, Vector2 b)
    {
        return Vector2.Distance(a, b);
    }

    public static T GetValue<T>(this WeakReference<T> wr) where T : class
    {
        T val;
        if (wr.TryGetTarget(out val)) { return val; }
        return null;
    }
}