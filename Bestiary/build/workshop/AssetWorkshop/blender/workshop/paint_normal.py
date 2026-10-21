"""The normal map the game's way: its normal maps are the albedo's own relief (bright raised, dark sunk; the normal's
x and y follow minus the value's slope along u and v with a correlation of 0.8 to 0.99 in most families, codex
paint.md). This makes that normal from a baked albedo, optionally on top of a baked geometric normal, at the family's
measured strength (normal units per unit of value per texel: about 2 for metal and cloth, 3 to 4 for wood, skin and
chitin, 5 to 7 for bone, bark and stone).

The bake's own bump cannot see what the game's normals carry most: the per-texel jitter and the hollows, worn edges
and occlusion painted into the albedo. So after the albedo bake (with its occlusion multiplied in), a pipeline that
wants the game's look calls image_from_albedo(albedo, normal, strength); tools without Blender call png_from_albedo.
Plain numpy; bpy only in image_from_albedo.
"""
import numpy as np


def from_albedo(rgb, strength, base=None):
    """(h, w, 3) sRGB albedo 0..1 (row 0 at the top) -> (h, w, 3) tangent-space normal 0..1, OpenGL (y up, the way
    Unity and Blender read it). `base` is an optional baked normal (same layout) whose tilt is kept and added to."""
    value = rgb.max(axis=-1)
    slope_u = (np.roll(value, -1, axis=1) - np.roll(value, 1, axis=1)) / 2.0
    slope_v = -(np.roll(value, -1, axis=0) - np.roll(value, 1, axis=0)) / 2.0
    x, y = -strength * slope_u, -strength * slope_v
    if base is not None:
        tilt = base * 2.0 - 1.0
        x, y = x + tilt[..., 0] / np.maximum(tilt[..., 2], 0.2), y + tilt[..., 1] / np.maximum(tilt[..., 2], 0.2)
    normal = np.stack([x, y, np.ones_like(x)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    return normal * 0.5 + 0.5


def image_from_albedo(albedo, normal, strength, keep_baked=True):
    """Writes the albedo-derived normal into the Blender image `normal` (Non-Color, same size as `albedo`)."""
    size = albedo.size[0], albedo.size[1]
    rgba = np.empty(size[0] * size[1] * 4, np.float32)
    albedo.pixels.foreach_get(rgba)
    rgb = rgba.reshape(size[1], size[0], 4)[::-1, :, :3]
    rgb = _to_srgb(rgb) if albedo.is_float else rgb
    base = None
    if keep_baked:
        baked = np.empty(size[0] * size[1] * 4, np.float32)
        normal.pixels.foreach_get(baked)
        base = baked.reshape(size[1], size[0], 4)[::-1, :, :3]
    made = from_albedo(rgb, strength, base)[::-1]
    out = np.concatenate([made, np.ones(made.shape[:2] + (1,))], axis=-1).astype(np.float32)
    normal.pixels.foreach_set(out.ravel())
    normal.update()


def png_from_albedo(albedo_png, normal_png, strength, base_png=None):
    """The same for PNG files (codex tools, no Blender)."""
    from PIL import Image
    with Image.open(albedo_png) as image:
        rgb = np.asarray(image.convert("RGB"), dtype=np.float64) / 255.0
    base = None
    if base_png:
        with Image.open(base_png) as image:
            base = np.asarray(image.convert("RGB").resize(rgb.shape[1::-1]), dtype=np.float64) / 255.0
    made = from_albedo(rgb, strength, base)
    Image.fromarray(np.round(made * 255).astype(np.uint8)).save(normal_png)


def _to_srgb(linear):
    """A float image's pixels are linear: back to sRGB values, as a PNG stores them (a byte image's already are)."""
    linear = np.clip(linear, 0.0, 1.0)
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * linear ** (1 / 2.4) - 0.055)
