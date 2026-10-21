using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>The spec's curves, colours and vectors as Unity's particle types.</summary>
    public static class VfxCurves
    {
        public static ParticleSystem.MinMaxCurve Curve(Curve c)
        {
            if (c == null)
                return new ParticleSystem.MinMaxCurve(0f);
            switch (c.mode)
            {
                case "range": return new ParticleSystem.MinMaxCurve(c.min, c.max);
                case "curve": return new ParticleSystem.MinMaxCurve(1f, Keys(c.t, c.v));
                case "curves": return new ParticleSystem.MinMaxCurve(1f, Keys(c.t, c.v), Keys(c.t2, c.v2));
                default: return new ParticleSystem.MinMaxCurve(c.c);
            }
        }

        /// <summary>An AnimationCurve through the keys with smooth tangents (the spec keeps samples, not tangents).</summary>
        public static AnimationCurve Keys(float[] t, float[] v)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < t.Length; i++)
                curve.AddKey(new Keyframe(t[i], v[i]));
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        public static ParticleSystem.MinMaxGradient Gradient(Grad g)
        {
            if (g == null)
                return new ParticleSystem.MinMaxGradient(Color.white);
            switch (g.mode)
            {
                case "colours": return new ParticleSystem.MinMaxGradient(Colour(g.a), Colour(g.b));
                case "gradient": return new ParticleSystem.MinMaxGradient(Make(g.ck, g.ak));
                case "gradients": return new ParticleSystem.MinMaxGradient(Make(g.ck, g.ak), Make(g.ck2, g.ak2));
                case "random":
                    return new ParticleSystem.MinMaxGradient(Make(g.ck, g.ak)) { mode = ParticleSystemGradientMode.RandomColor };
                default: return new ParticleSystem.MinMaxGradient(Colour(g.a));
            }
        }

        /// <summary>A Gradient from flat colour keys (t, r, g, b) and alpha keys (t, a).</summary>
        public static Gradient Make(float[] colourKeys, float[] alphaKeys)
        {
            var colours = new GradientColorKey[Mathf.Max(1, colourKeys.Length / 4)];
            for (int i = 0; i < colourKeys.Length / 4; i++)
                colours[i] = new GradientColorKey(new Color(colourKeys[i * 4 + 1], colourKeys[i * 4 + 2], colourKeys[i * 4 + 3]), colourKeys[i * 4]);
            if (colourKeys.Length < 4)
                colours[0] = new GradientColorKey(Color.white, 0f);
            var alphas = new GradientAlphaKey[Mathf.Max(1, alphaKeys.Length / 2)];
            for (int i = 0; i < alphaKeys.Length / 2; i++)
                alphas[i] = new GradientAlphaKey(alphaKeys[i * 2 + 1], alphaKeys[i * 2]);
            if (alphaKeys.Length < 2)
                alphas[0] = new GradientAlphaKey(1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(colours, alphas);
            return gradient;
        }

        public static Color Colour(float[] v) =>
            v == null || v.Length < 3 ? Color.white : new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f);

        public static Vector3 Vector(float[] v) => v == null || v.Length < 3 ? Vector3.zero : new Vector3(v[0], v[1], v[2]);
    }
}
