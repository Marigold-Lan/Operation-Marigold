using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = OperationMarigoldPaths.SoBuildingData)]
public class BuildingData : ScriptableObject
{
    [Header("基础信息")]
    public string id;
    public string displayName;

    [Header("收入")]
    [Tooltip("每回合提供的资金。")]
    public int incomePerTurn;

    [Header("占领")]
    [Tooltip("占领耐久度上限，步兵踩一次减少 captureDamagePerStep。")]
    public int maxCaptureHp = 20;
    [Tooltip("每次占领行动造成的耐久度伤害。")]
    public int captureDamagePerStep = 1;

    [Header("胜负")]
    [Tooltip("是否为总部。总部陷落影响胜负。")]
    public bool isHq;

    [Header("工厂（可选）")]
    [Tooltip("仅工厂类建筑使用：工厂可制造单位目录（按阵营）。为空则由 UI/默认配置回退处理。")]
    public FactoryBuildCatalogSO factoryBuildCatalog;

    [Header("外观")]
    [Tooltip("未被占领（None）时使用的默认 Mesh。若为空则保留原 Mesh。")]
    public Mesh defaultMesh;
    [Tooltip("不同阵营对应的建筑 Mesh。")]
    public List<FactionMeshEntry> factionMeshes = new List<FactionMeshEntry>();

    public bool TryGetMesh(UnitFaction faction, out Mesh mesh)
    {
        for (var i = 0; i < factionMeshes.Count; i++)
        {
            var entry = factionMeshes[i];
            if (entry.faction == faction && entry.mesh != null)
            {
                mesh = entry.mesh;
                return true;
            }
        }

        if (faction == UnitFaction.None && defaultMesh != null)
        {
            mesh = defaultMesh;
            return true;
        }

        mesh = null;
        return false;
    }
}

[System.Serializable]
public struct FactionMeshEntry
{
    public UnitFaction faction;
    public Mesh mesh;
}
