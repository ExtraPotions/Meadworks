// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackNamedEffectSystem.cs. Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespaces, canonical catalogs, Crucible settings and diagnostics;
// native source algorithms retained. Candidate stage, dispatch and prefab application are a separate unit.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Config;

internal static class SecondaryAttackNamedEffectSystem
{
    private const float EffectLifetime = 10f;
    private static readonly Dictionary<string, GameObject> PrefabCache = new();

    internal static bool Create(
        string? prefabName,
        Vector3 position,
        Quaternion rotation,
        string context,
        float lifetime = EffectLifetime)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            return false;
        }

        string trimmedPrefabName = prefabName!.Trim();
        if (!TryResolvePrefab(trimmedPrefabName, out GameObject? resolvedPrefab))
        {
            if (SecondaryAttackWarningLog.TryMarkWarning($"named_effect_missing_{context}_{trimmedPrefabName}"))
            {
                RuleDiagnostics.Warning($"Configured effect prefab '{trimmedPrefabName}' was not found for {context}.");
            }

            return false;
        }

        GameObject prefab = resolvedPrefab!;
        GameObject instance = Object.Instantiate(prefab, position, rotation);
        Object.Destroy(instance, lifetime);
        return true;
    }

    private static bool TryResolvePrefab(string prefabName, out GameObject? prefab)
    {
        if (PrefabCache.TryGetValue(prefabName, out prefab))
        {
            if (prefab != null)
            {
                return true;
            }

            PrefabCache.Remove(prefabName);
        }

        prefab = ZNetScene.instance?.GetPrefab(prefabName);
        if (prefab == null)
        {
            return false;
        }

        PrefabCache[prefabName] = prefab;
        return true;
    }

    internal static Quaternion RotationFromNormal(Vector3 normal)
    {
        return normal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(normal.normalized)
            : Quaternion.identity;
    }

    internal static void ScaleParticleSystems(GameObject instance, float scale)
    {
        if (Mathf.Approximately(scale, 1f))
        {
            return;
        }

        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particleSystem.main;
            if (main.startSize3D)
            {
                main.startSizeX = ScaleCurve(main.startSizeX, scale);
                main.startSizeY = ScaleCurve(main.startSizeY, scale);
                main.startSizeZ = ScaleCurve(main.startSizeZ, scale);
            }
            else
            {
                main.startSize = ScaleCurve(main.startSize, scale);
            }

            main.startSpeed = ScaleCurve(main.startSpeed, scale);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            if (shape.enabled)
            {
                shape.radius *= scale;
                shape.scale *= scale;
                shape.position *= scale;
            }

            ParticleSystem.TrailModule trails = particleSystem.trails;
            if (trails.enabled)
            {
                trails.widthOverTrail = ScaleCurve(trails.widthOverTrail, scale);
            }

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.lengthScale *= scale;
                renderer.velocityScale *= scale;
            }
        }
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float scale)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                curve.constant *= scale;
                break;
            case ParticleSystemCurveMode.TwoConstants:
                curve.constantMin *= scale;
                curve.constantMax *= scale;
                break;
            case ParticleSystemCurveMode.Curve:
            case ParticleSystemCurveMode.TwoCurves:
                curve.curveMultiplier *= scale;
                break;
        }

        return curve;
    }
}
