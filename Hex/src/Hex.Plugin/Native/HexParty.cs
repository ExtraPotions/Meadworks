// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using Object = UnityEngine.Object;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexParty
{
    private static readonly List<ParticleSystem> bursts = new();
    private static readonly System.Random random = new();
    private static readonly Color[] colors = { new(1f, 0.65f, 0.1f), new(1f, 0.3f, 0.4f), new(0.3f, 0.8f, 1f), new(0.4f, 1f, 0.5f), new(0.8f, 0.4f, 1f) };
    private static Material? material;
    private static ManualLogSource? log;
    private static Player? actor;
    private static long generation;
    private static bool warned;

    internal static void Initialize(ManualLogSource logger) { log = logger; warned = false; }
    internal static void Reset()
    {
        foreach (var burst in bursts) if (burst != null) Object.Destroy(burst.gameObject);
        bursts.Clear();
        if (material != null) Object.Destroy(material);
        material = null; actor = null;
    }
    internal static void Shutdown() { Reset(); log = null; }
    internal static void Tick()
    {
        if ((actor != null || material != null || bursts.Count > 0) && (actor == null || actor != Player.m_localPlayer || generation != CruciblePlugin.Session.Generation
            || !HexMovement.On(actor, "party_hits"))) { Reset(); return; }
        for (var i = bursts.Count - 1; i >= 0; i--)
            if (bursts[i] == null || !bursts[i].IsAlive())
            {
                if (bursts[i] != null) Object.Destroy(bursts[i].gameObject);
                bursts.RemoveAt(i);
            }
    }
    private static bool Finite(Vector3 point) => !(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z)
        || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z));
    private static void Emit(Vector3 point)
    {
        GameObject? effect = null;
        try
        {
            Tick();
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException();
                material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            }
            if (bursts.Count >= 16) { if (bursts[0] != null) Object.Destroy(bursts[0].gameObject); bursts.RemoveAt(0); }
            effect = new GameObject("Meadworks.Hex.PartyHit");
            effect.transform.position = point;
            var particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = false; main.duration = 2.5f; main.startLifetime = 2.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.35f;
            main.maxParticles = 28; main.startSize3D = true; main.startSizeX = 0.06f; main.startSizeY = 0.12f;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            var overLife = particles.colorOverLifetime; overLife.enabled = true; overLife.color = fade;
            var rotation = particles.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            actor = Player.m_localPlayer; generation = CruciblePlugin.Session.Generation;
            particles.Play();
            for (var i = 0; i < 28; i++)
                particles.Emit(new ParticleSystem.EmitParams { startColor = colors[random.Next(colors.Length)],
                    velocity = new Vector3((float)random.NextDouble() * 3f - 1.5f, (float)random.NextDouble() * 2f + 1f, (float)random.NextDouble() * 3f - 1.5f),
                    rotation = (float)random.NextDouble() * 360f }, 1);
            bursts.Add(particles);
        }
        catch
        {
            if (effect != null) Object.Destroy(effect);
            if (!warned) { warned = true; log?.LogWarning("Hex Party Hits visual unavailable; native hits continue."); }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    private static class CreatureHit
    {
        private static void Prefix(Character __instance, HitData hit, out bool __state)
        {
            __state = MeadworksHeadless.AllowsUi && __instance != null && !__instance.IsPlayer() && !__instance.IsDead()
                && __instance.m_nview != null && __instance.m_nview.IsValid() && hit != null && Finite(hit.m_point)
                && HexMovement.On(hit.GetAttacker(), "party_hits");
        }
        private static void Postfix(HitData hit, bool __state)
        {
            if (__state && hit != null && Finite(hit.m_point) && HexMovement.On(hit.GetAttacker(), "party_hits")) Emit(hit.m_point);
        }
    }
}
