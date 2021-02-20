using NostalgiaOrbitDLL.Environments;
using System;
using UnityEngine;

[Serializable]
public class StandardEnvironmentObject : AbstractEnvironmentObject
{
    public StandardEnvironmentObject(AbstractEnvironment abstractEnvironment, Vector2 position)
    {
        Id = Guid.NewGuid();

        AbstractEnvironment = abstractEnvironment;
        PositionObj = position.ToPositionVector();
        Position = position;
        PrefabType = abstractEnvironment.OccupiedPrefabType ?? abstractEnvironment.PrefabType;
    }
}