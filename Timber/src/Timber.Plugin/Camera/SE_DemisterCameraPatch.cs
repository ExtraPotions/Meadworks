#nullable disable
// Cartur Build Camera. SPDX-License-Identifier: GPL-3.0-only
// Adopted from jekkle/cartur-build-camera f81977b2553d48a2a79eea62969f86bb9d9fa32e / src/SE_DemisterCameraPatch.cs.
// Changed by expDARE, 2026: Timber effective settings, native lifecycle and owned cleanup.
using HarmonyLib;
using UnityEngine;

namespace Timber.Camera
{
	[HarmonyPatch(typeof(SE_Demister), nameof(SE_Demister.UpdateStatusEffect))]
	public static class SE_DemisterCameraPatch
	{
		private static GameObject _trackedBall = null;
		private static ParticleSystemForceField _trackedForceField = null;
		private static float _originalForceFieldRange;
        private static float _writtenForceFieldRange;

		public static bool Prefix(SE_Demister __instance, float dt)
		{
			Player player = __instance.m_character as Player;
            if (player != Player.m_localPlayer) return true;
			GameCamera camera = GameCamera.instance;
			if (!player || !camera || !CameraState.Active || !CameraSettings.DemisterFollow.Value)
			{
				RestoreNormalForceField();
				return true;
			}

			DemisterAge.Update(__instance, dt);
            UpdateDemisterForCamera(__instance, player, camera.transform, dt);
            UpdateForceFieldRange(__instance);

			return false;
		}

		private static void UpdateForceFieldRange(SE_Demister instance)
		{
			GameObject ball = instance.m_ballInstance;
			if (!ball) return;

			if (_trackedBall != ball || !_trackedForceField)
			{
				RestoreNormalForceField();
				ParticleSystemForceField forceField = ball.GetComponentInChildren<ParticleSystemForceField>();
				if (!forceField) return;
				_trackedBall = ball;
				_trackedForceField = forceField;
				_originalForceFieldRange = forceField.endRange;
                _writtenForceFieldRange = forceField.endRange;
			}

			if (!Mathf.Approximately(_trackedForceField.endRange, _writtenForceFieldRange))
                _originalForceFieldRange = _trackedForceField.endRange;
            _writtenForceFieldRange = _originalForceFieldRange * CameraSettings.DemisterRange.Value;
            _trackedForceField.endRange = _writtenForceFieldRange;
		}

		private static void RestoreNormalForceField()
		{
			if (_trackedForceField && Mathf.Approximately(_trackedForceField.endRange, _writtenForceFieldRange))
                _trackedForceField.endRange = _originalForceFieldRange;
			_trackedBall = null;
			_trackedForceField = null;
			_originalForceFieldRange = 0f;
		}

		private static void UpdateDemisterForCamera(SE_Demister instance, Player player, Transform camera, float dt)
		{
			Vector3 cameraPosition = camera.position;
			Vector3 cameraForward = camera.forward;

			if (!instance.m_ballInstance)
			{
				instance.m_ballInstance = Object.Instantiate(instance.m_ballPrefab, cameraPosition + cameraForward * 0.5f, Quaternion.identity);
				return;
			}

			bool isUnderRoof = IsUnderRoof(instance, cameraPosition);
			Transform ballTransform = instance.m_ballInstance.transform;
			Vector3 ballPosition = ballTransform.position;
			Vector3 offset = isUnderRoof ? instance.m_offsetInterior : instance.m_offset;
			float noiseDistance = isUnderRoof ? instance.m_noiseDistanceInterior : instance.m_noiseDistance;
			Vector3 transformedOffset = camera.right * offset.x + camera.up * offset.y + cameraForward * offset.z;
			Vector3 targetPosition = cameraPosition + transformedOffset;
			float time = Time.time * instance.m_noiseSpeed;
			Vector3 noiseOffset = new Vector3(Mathf.Sin(time * 4f), Mathf.Sin(time * 2f) * instance.m_noiseDistanceYScale, Mathf.Cos(time * 5f)) * noiseDistance;
			Vector3 targetWithNoise = targetPosition + noiseOffset;
			Vector3 ballToTarget = targetWithNoise - ballPosition;
			float distanceSquared = ballToTarget.sqrMagnitude;
			float maxDistanceSquared = instance.m_maxDistance * instance.m_maxDistance;

			if (distanceSquared > maxDistanceSquared * 4f)
			{
				ballPosition = targetWithNoise;
			}
			else if (distanceSquared > maxDistanceSquared)
			{
				ballPosition = targetWithNoise - ballToTarget * (instance.m_maxDistance / Mathf.Sqrt(distanceSquared));
			}

			ballToTarget = targetWithNoise - ballPosition;
			float distanceToTargetSquared = ballToTarget.sqrMagnitude;
			if (distanceToTargetSquared > 0.000001f)
				instance.m_ballVel += ballToTarget * (instance.m_ballAcceleration * dt / Mathf.Sqrt(distanceToTargetSquared));

			float speedSquared = instance.m_ballVel.sqrMagnitude;
			float maxSpeedSquared = instance.m_ballMaxSpeed * instance.m_ballMaxSpeed;
			if (speedSquared > maxSpeedSquared)
			{
				instance.m_ballVel *= instance.m_ballMaxSpeed / Mathf.Sqrt(speedSquared);
			}

			if (!isUnderRoof)
			{
				instance.m_ballVel += player.GetVelocity() * instance.m_characterVelocityFactor * dt;
			}

			instance.m_ballVel -= instance.m_ballVel * instance.m_ballFriction;

			ballTransform.position = ballPosition + instance.m_ballVel * dt;
			ballTransform.rotation *= Quaternion.Euler(instance.m_rotationSpeed, 0.0f, instance.m_rotationSpeed * 0.5321f);
		}

		private static bool IsUnderRoof(SE_Demister instance, Vector3 cameraPosition)
		{
			return Physics.Raycast(cameraPosition, Vector3.up, out RaycastHit _, 4f, instance.m_coverRayMask);
		}

		internal static void CleanupBall(GameObject ball)
		{
			if (_trackedBall == ball) RestoreNormalForceField();
		}

		internal static void Cleanup()
		{
			RestoreNormalForceField();
		}
	}

	[HarmonyPatch(typeof(SE_Demister), nameof(SE_Demister.RemoveEffects))]
	public static class SE_DemisterCleanupPatch
	{
		[HarmonyPrefix]
		public static void RemoveEffects_Prefix(SE_Demister __instance)
		{
			SE_DemisterCameraPatch.CleanupBall(__instance.m_ballInstance);
		}
	}
}

namespace Timber.Camera
{
    [HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.UpdateStatusEffect))]
    internal static class DemisterAge
    {
        // Invoke the native base implementation once; the follow override replaces only ball movement.
        [HarmonyReversePatch]
        internal static void Update(StatusEffect instance, float dt) => throw new System.NotImplementedException("Harmony reverse patch");
    }
}
