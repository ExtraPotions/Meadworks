// Cartur Build Camera. SPDX-License-Identifier: GPL-3.0-only
// Adopted from jekkle/cartur-build-camera f81977b2553d48a2a79eea62969f86bb9d9fa32e / src/Utils.cs.
// Changed by expDARE, 2026: apply separate axis polarity to native processed input; preserve sensitivity, gyro and native inversion.
using UnityEngine;

namespace Timber.Camera
{
    internal static class CarturLook
    {
        internal static Vector2 Adjust(Vector2 look)
        {
            // Native PlayerController chooses the mouse branch before the gamepad branch.
            bool mouse = ZInput.IsMouseActive();
            bool invertH = mouse ? CameraPrefs.InvertMouseHorizontal.Value : CameraPrefs.InvertPadHorizontal.Value;
            bool invertV = mouse ? CameraPrefs.InvertMouseVertical.Value : CameraPrefs.InvertPadVertical.Value;
            if (invertH) look.x *= -1f;
            if (invertV) look.y *= -1f;
            return look;
        }
    }
}
