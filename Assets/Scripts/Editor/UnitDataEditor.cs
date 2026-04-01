#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using OperationMarigold.AI.Minimax;

[CustomEditor(typeof(UnitData))]
[CanEditMultipleObjects]
public class UnitDataEditor : Editor
{
    private SerializedProperty _idProp;
    private SerializedProperty _displayNameProp;
    private SerializedProperty _descriptionProp;
    private SerializedProperty _costProp;
    private SerializedProperty _prefabProp;
    private SerializedProperty _maxHpProp;
    private SerializedProperty _maxFuelProp;
    private SerializedProperty _movementRangeProp;
    private SerializedProperty _movementTypeProp;
    private SerializedProperty _visionRangeProp;
    private SerializedProperty _categoryProp;
    private SerializedProperty _aiRoleOverrideProp;

    private SerializedProperty _hasPrimaryWeaponProp;
    private SerializedProperty _primaryWeaponProp;
    private SerializedProperty _primaryAmmoCapacityProp;

    private SerializedProperty _hasSecondaryWeaponProp;
    private SerializedProperty _secondaryWeaponProp;

    private bool _showPrimaryWeapon = true;
    private bool _showSecondaryWeapon = true;

    private void OnEnable()
    {
        _idProp = serializedObject.FindProperty("id");
        _displayNameProp = serializedObject.FindProperty("displayName");
        _descriptionProp = serializedObject.FindProperty("Description");
        _costProp = serializedObject.FindProperty("cost");
        _prefabProp = serializedObject.FindProperty("prefab");
        _maxHpProp = serializedObject.FindProperty("maxHp");
        _maxFuelProp = serializedObject.FindProperty("maxFuel");
        _movementRangeProp = serializedObject.FindProperty("movementRange");
        _movementTypeProp = serializedObject.FindProperty("movementType");
        _visionRangeProp = serializedObject.FindProperty("visionRange");
        _categoryProp = serializedObject.FindProperty("category");
        _aiRoleOverrideProp = serializedObject.FindProperty("aiRoleOverride");

        _hasPrimaryWeaponProp = serializedObject.FindProperty("hasPrimaryWeapon");
        _primaryWeaponProp = serializedObject.FindProperty("primaryWeapon");
        _primaryAmmoCapacityProp = serializedObject.FindProperty("primaryAmmoCapacity");

        _hasSecondaryWeaponProp = serializedObject.FindProperty("hasSecondaryWeapon");
        _secondaryWeaponProp = serializedObject.FindProperty("secondaryWeapon");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawBasicSection();
        EditorGUILayout.Space(4f);
        DrawWeaponSection(
            "主武器槽位（有限弹药）",
            isPrimary: true,
            _hasPrimaryWeaponProp,
            _primaryWeaponProp,
            _primaryAmmoCapacityProp);

        EditorGUILayout.Space(4f);
        DrawWeaponSection(
            "副武器槽位（无限弹药）",
            isPrimary: false,
            _hasSecondaryWeaponProp,
            _secondaryWeaponProp,
            ammoProp: null);

        DrawWeaponValidationHint();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBasicSection()
    {
        EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_idProp);
        EditorGUILayout.PropertyField(_displayNameProp);
        EditorGUILayout.PropertyField(_descriptionProp);
        EditorGUILayout.PropertyField(_costProp);
        EditorGUILayout.PropertyField(_prefabProp);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("基础能力", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_maxHpProp);
        EditorGUILayout.PropertyField(_maxFuelProp);
        EditorGUILayout.PropertyField(_movementRangeProp);
        EditorGUILayout.PropertyField(_movementTypeProp);
        EditorGUILayout.PropertyField(_visionRangeProp);
        EditorGUILayout.PropertyField(_categoryProp);

        if (_aiRoleOverrideProp != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("AI 角色(覆盖)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_aiRoleOverrideProp);

            if (!_aiRoleOverrideProp.hasMultipleDifferentValues)
            {
                var tag = (UnitAIRoleTag)_aiRoleOverrideProp.enumValueIndex;
                if (tag == UnitAIRoleTag.Auto)
                {
                    EditorGUILayout.HelpBox(
                        "Auto：AI 会从组件/属性推断该单位可同时属于多个部队（如步兵=占领/辅助，APC=补给/运输）。",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"已启用覆盖：本单位将被强制归入 `{tag}` 对应的单一部队角色（生产/编制决策）。",
                        MessageType.Warning);
                }
            }

            // 给单个对象展示推断预览，便于你快速判断“为什么它会出现在某类部队里”
            if (!serializedObject.isEditingMultipleObjects && _aiRoleOverrideProp.serializedObject.targetObject is UnitData ud)
            {
                var caps = AIUnitRoleClassifier.GetCapabilitiesForProduction(ud);
                EditorGUILayout.LabelField("推断能力预览", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(FormatCapabilities(caps), MessageType.None);
            }
        }
    }

    private static string FormatCapabilities(AIUnitRoleCapabilities caps)
    {
        if (caps == AIUnitRoleCapabilities.None)
            return "无（可能缺少 prefab 或未配置/推断所需组件）。";

        // Flat list to avoid nested bullets in Inspector
        string s = "";
        if ((caps & AIUnitRoleCapabilities.CaptureTeam) != 0) s += (s.Length == 0 ? "" : ", ") + "CaptureTeam";
        if ((caps & AIUnitRoleCapabilities.AssaultSupport) != 0) s += (s.Length == 0 ? "" : ", ") + "AssaultSupport";
        if ((caps & AIUnitRoleCapabilities.AssaultMain) != 0) s += (s.Length == 0 ? "" : ", ") + "AssaultMain";
        if ((caps & AIUnitRoleCapabilities.RangedStrike) != 0) s += (s.Length == 0 ? "" : ", ") + "RangedStrike";
        if ((caps & AIUnitRoleCapabilities.TransportLogistics) != 0) s += (s.Length == 0 ? "" : ", ") + "TransportLogistics";
        if ((caps & AIUnitRoleCapabilities.SupplyLogistics) != 0) s += (s.Length == 0 ? "" : ", ") + "SupplyLogistics";
        return s;
    }

    private void DrawWeaponSection(string title, bool isPrimary, SerializedProperty hasWeaponProp, SerializedProperty weaponProp, SerializedProperty ammoProp)
    {
        var foldoutState = isPrimary ? _showPrimaryWeapon : _showSecondaryWeapon;
        foldoutState = EditorGUILayout.Foldout(foldoutState, title, true);
        if (isPrimary)
            _showPrimaryWeapon = foldoutState;
        else
            _showSecondaryWeapon = foldoutState;

        if (!foldoutState)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(hasWeaponProp, new GUIContent("启用该槽位"));
            var enabled = !hasWeaponProp.hasMultipleDifferentValues && hasWeaponProp.boolValue;

            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (ammoProp != null)
                    EditorGUILayout.PropertyField(ammoProp, new GUIContent("主武器弹药容量"));

                DrawWeaponFields(weaponProp);
            }

            if (!enabled && !hasWeaponProp.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox("该槽位未启用，下面武器配置不会在战斗中生效。", MessageType.Info);
        }
    }

    private static void DrawWeaponFields(SerializedProperty weaponProp)
    {
        if (weaponProp == null)
            return;

        var weaponName = weaponProp.FindPropertyRelative("weaponName");
        var baseDamage = weaponProp.FindPropertyRelative("baseDamage");
        var attackRangeMin = weaponProp.FindPropertyRelative("attackRangeMin");
        var attackRangeMax = weaponProp.FindPropertyRelative("attackRangeMax");
        var canAttackVehicle = weaponProp.FindPropertyRelative("canAttackVehicle");
        var canAttackSoldier = weaponProp.FindPropertyRelative("canAttackSoldier");
        var requiresStationaryToAttack = weaponProp.FindPropertyRelative("requiresStationaryToAttack");
        var damageMatrix = weaponProp.FindPropertyRelative("damageMatrix");

        EditorGUILayout.PropertyField(weaponName, new GUIContent("武器名称"));
        EditorGUILayout.PropertyField(baseDamage, new GUIContent("基础伤害"));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(attackRangeMin, new GUIContent("最小射程"));
        EditorGUILayout.PropertyField(attackRangeMax, new GUIContent("最大射程"));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(canAttackVehicle, new GUIContent("可攻击 Vehicle"));
        EditorGUILayout.PropertyField(canAttackSoldier, new GUIContent("可攻击 Soldier"));
        EditorGUILayout.PropertyField(requiresStationaryToAttack, new GUIContent("需架设开火（移动后不可攻击）"));
        EditorGUILayout.PropertyField(damageMatrix, new GUIContent("伤害矩阵"), includeChildren: true);
    }

    private void DrawWeaponValidationHint()
    {
        if (_hasPrimaryWeaponProp.hasMultipleDifferentValues || _hasSecondaryWeaponProp.hasMultipleDifferentValues)
            return;

        if (_hasPrimaryWeaponProp.boolValue || _hasSecondaryWeaponProp.boolValue)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox("当前单位未启用任何武器槽位，将无法进行攻击。", MessageType.Warning);
    }
}
#endif
