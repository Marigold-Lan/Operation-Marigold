using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Cell))]
public class CellView : MonoBehaviour
{
    [Header("透明触发地形")]
    [Tooltip("当本格 id 命中列表时，单位进入该格会触发透明。")]
    [SerializeField] private string[] _transparentTerrainIds =
    {
        "Forest", "Woods", "Mountain", "Mountains",
        "City", "HQ", "Factory", "Airport", "Lab", "CommTower", "Silo"
    };

    private const float TransparentAlpha = 0.25f;

    private Cell _cell;
    private bool _isSubscribed;
    private Coroutine _alphaCoroutine;

    private readonly List<MaterialState> _materialStates = new List<MaterialState>();
    private readonly HashSet<string> _terrainIdSet = new HashSet<string>();
    private Transform _placeableRoot;

    private struct MaterialState
    {
        public Material Material;
        public bool HasSurface;
        public float Surface;
        public bool HasBlend;
        public float Blend;
        public bool HasZWrite;
        public float ZWrite;
        public bool HasSrcBlend;
        public float SrcBlend;
        public bool HasDstBlend;
        public float DstBlend;
        public bool HasBaseColor;
        public Color BaseColor;
        public bool HasColor;
        public Color Color;
        public int RenderQueue;
    }

    private void Awake()
    {
        _cell = GetComponent<Cell>();
        BuildTerrainIdSet();
        CachePlaceableFirstMaterials();
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshByOccupancy();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopAlphaTween();
        RestoreOpaque();
    }

    private void OnValidate()
    {
        BuildTerrainIdSet();
    }

    private void BuildTerrainIdSet()
    {
        _terrainIdSet.Clear();
        if (_transparentTerrainIds == null) return;

        for (var i = 0; i < _transparentTerrainIds.Length; i++)
        {
            var id = _transparentTerrainIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                _terrainIdSet.Add(id.Trim());
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed || _cell == null) return;
        _cell.OnUnitWillEnter += HandleUnitWillEnter;
        _cell.OnUnitWillLeave += HandleUnitWillLeave;
        _cell.OnUnitEntered += HandleUnitEntered;
        _cell.OnUnitLeft += HandleUnitLeft;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _cell == null) return;
        _cell.OnUnitWillEnter -= HandleUnitWillEnter;
        _cell.OnUnitWillLeave -= HandleUnitWillLeave;
        _cell.OnUnitEntered -= HandleUnitEntered;
        _cell.OnUnitLeft -= HandleUnitLeft;
        _isSubscribed = false;
    }

    private void HandleUnitWillEnter(Cell cell, GameObject unit, float duration)
    {
        if (!ShouldBecomeTransparent()) return;
        FadeToTransparent(duration);
    }

    private void HandleUnitWillLeave(Cell cell, GameObject unit, float duration)
    {
        FadeToOpaque(duration);
    }

    private void HandleUnitEntered(Cell cell, GameObject unit)
    {
        if (ShouldBecomeTransparent())
            ApplyTransparentImmediate();
    }

    private void HandleUnitLeft(Cell cell, GameObject unit)
    {
        RestoreOpaqueImmediate();
    }

    private void RefreshByOccupancy()
    {
        if (_cell != null && _cell.HasUnit && ShouldBecomeTransparent())
            ApplyTransparentImmediate();
        else
            RestoreOpaqueImmediate();
    }

    private bool ShouldBecomeTransparent()
    {
        if (_cell == null) return false;
        if (_cell.PlaceableType == null) return false;
        if (_cell.Building != null) return true;

        var placeableId = _cell.PlaceableType != null ? _cell.PlaceableType.id : null;
        if (!string.IsNullOrEmpty(placeableId) && _terrainIdSet.Contains(placeableId))
            return true;

        return false;
    }

    private void CachePlaceableFirstMaterials()
    {
        _placeableRoot = ResolvePlaceableRoot();
        _materialStates.Clear();
        if (_placeableRoot == null) return;

        var renderers = _placeableRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            var mats = renderer.materials;
            if (mats == null || mats.Length == 0 || mats[0] == null) continue;
            _materialStates.Add(CreateMaterialState(mats[0]));
        }
    }

    private Transform ResolvePlaceableRoot()
    {
        if (_cell == null || _cell.PlaceableType == null || _cell.PlaceableType.prefab == null)
            return null;

        var expectedName = _cell.PlaceableType.prefab.name;
        Transform fallback = null;

        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;
            if (fallback == null) fallback = child;

            var strippedName = child.name.Replace("(Clone)", string.Empty).Trim();
            if (strippedName == expectedName)
                return child;
        }

        return fallback;
    }

    private static MaterialState CreateMaterialState(Material material)
    {
        var state = new MaterialState
        {
            Material = material,
            HasSurface = material.HasProperty("_Surface"),
            HasBlend = material.HasProperty("_Blend"),
            HasZWrite = material.HasProperty("_ZWrite"),
            HasSrcBlend = material.HasProperty("_SrcBlend"),
            HasDstBlend = material.HasProperty("_DstBlend"),
            HasBaseColor = material.HasProperty("_BaseColor"),
            HasColor = material.HasProperty("_Color"),
            RenderQueue = material.renderQueue
        };

        if (state.HasSurface) state.Surface = material.GetFloat("_Surface");
        if (state.HasBlend) state.Blend = material.GetFloat("_Blend");
        if (state.HasZWrite) state.ZWrite = material.GetFloat("_ZWrite");
        if (state.HasSrcBlend) state.SrcBlend = material.GetFloat("_SrcBlend");
        if (state.HasDstBlend) state.DstBlend = material.GetFloat("_DstBlend");
        if (state.HasBaseColor) state.BaseColor = material.GetColor("_BaseColor");
        if (state.HasColor) state.Color = material.GetColor("_Color");
        return state;
    }

    private void ApplyTransparentImmediate()
    {
        StopAlphaTween();
        if (!EnsurePlaceableMaterials()) return;
        PrepareTransparentRenderState();
        SetAlphaNow(TransparentAlpha);
    }

    private void RestoreOpaqueImmediate()
    {
        StopAlphaTween();
        RestoreOpaque();
    }

    private void FadeToTransparent(float duration)
    {
        if (!EnsurePlaceableMaterials()) return;
        StopAlphaTween();
        PrepareTransparentRenderState();

        if (duration <= 0f)
        {
            SetAlphaNow(TransparentAlpha);
            return;
        }

        _alphaCoroutine = StartCoroutine(FadeAlphaCoroutine(toTransparent: true, duration));
    }

    private void FadeToOpaque(float duration)
    {
        if (!EnsurePlaceableMaterials())
        {
            RestoreOpaque();
            return;
        }

        StopAlphaTween();
        PrepareTransparentRenderState();

        if (duration <= 0f)
        {
            RestoreOpaque();
            return;
        }

        _alphaCoroutine = StartCoroutine(FadeAlphaCoroutine(toTransparent: false, duration));
    }

    private IEnumerator FadeAlphaCoroutine(bool toTransparent, float duration)
    {
        var count = _materialStates.Count;
        var startBase = new Color[count];
        var endBase = new Color[count];
        var useBase = new bool[count];
        var startColor = new Color[count];
        var endColor = new Color[count];
        var useColor = new bool[count];

        for (var i = 0; i < count; i++)
        {
            var state = _materialStates[i];
            var material = state.Material;
            if (material == null) continue;

            if (state.HasBaseColor)
            {
                useBase[i] = true;
                startBase[i] = material.GetColor("_BaseColor");
                endBase[i] = toTransparent ? WithAlpha(state.BaseColor, TransparentAlpha) : state.BaseColor;
            }

            if (state.HasColor)
            {
                useColor[i] = true;
                startColor[i] = material.GetColor("_Color");
                endColor[i] = toTransparent ? WithAlpha(state.Color, TransparentAlpha) : state.Color;
            }
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = elapsed / duration;
            if (t > 1f) t = 1f;

            for (var i = 0; i < count; i++)
            {
                var state = _materialStates[i];
                var material = state.Material;
                if (material == null) continue;

                if (useBase[i]) material.SetColor("_BaseColor", Color.Lerp(startBase[i], endBase[i], t));
                if (useColor[i]) material.SetColor("_Color", Color.Lerp(startColor[i], endColor[i], t));
            }

            yield return null;
        }

        _alphaCoroutine = null;
        if (toTransparent)
        {
            SetAlphaNow(TransparentAlpha);
        }
        else
        {
            RestoreOpaque();
        }
    }

    private static Color WithAlpha(Color src, float alpha)
    {
        src.a = alpha;
        return src;
    }

    private bool EnsurePlaceableMaterials()
    {
        if (_placeableRoot == null || _materialStates.Count == 0)
            CachePlaceableFirstMaterials();
        return _placeableRoot != null && _materialStates.Count > 0;
    }

    private void PrepareTransparentRenderState()
    {
        for (var i = 0; i < _materialStates.Count; i++)
        {
            var state = _materialStates[i];
            var material = state.Material;
            if (material == null) continue;

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            if (state.HasSurface) material.SetFloat("_Surface", 1f);
            if (state.HasBlend) material.SetFloat("_Blend", 0f);
            if (state.HasZWrite) material.SetFloat("_ZWrite", 0f);
            if (state.HasSrcBlend) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (state.HasDstBlend) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }

    private void SetAlphaNow(float alpha)
    {
        for (var i = 0; i < _materialStates.Count; i++)
        {
            var state = _materialStates[i];
            var material = state.Material;
            if (material == null) continue;

            if (state.HasBaseColor)
            {
                var c = material.GetColor("_BaseColor");
                c.a = alpha;
                material.SetColor("_BaseColor", c);
            }

            if (state.HasColor)
            {
                var c = material.GetColor("_Color");
                c.a = alpha;
                material.SetColor("_Color", c);
            }
        }
    }

    private void RestoreOpaque()
    {
        for (var i = 0; i < _materialStates.Count; i++)
        {
            var state = _materialStates[i];
            var material = state.Material;
            if (material == null) continue;

            if (state.HasSurface)
            {
                material.SetFloat("_Surface", state.Surface);
                if (state.Surface < 0.5f)
                {
                    material.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                else
                {
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                }
            }

            if (state.HasBlend) material.SetFloat("_Blend", state.Blend);
            if (state.HasZWrite) material.SetFloat("_ZWrite", state.ZWrite);
            if (state.HasSrcBlend) material.SetFloat("_SrcBlend", state.SrcBlend);
            if (state.HasDstBlend) material.SetFloat("_DstBlend", state.DstBlend);
            material.renderQueue = state.RenderQueue;

            if (state.HasBaseColor) material.SetColor("_BaseColor", state.BaseColor);
            if (state.HasColor) material.SetColor("_Color", state.Color);
        }

    }

    private void StopAlphaTween()
    {
        if (_alphaCoroutine == null) return;
        StopCoroutine(_alphaCoroutine);
        _alphaCoroutine = null;
    }
}
