using System;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  数学函数库（模仿 V2 FunctionsMath 全部函数）
    // ====================================================================
    public static class SfmExtMath
    {
        [SfmExtFunction("math.sin")] public static SfmExtValue FnSin(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Sin(p.Get("value").ToFloat()));
        [SfmExtFunction("math.cos")] public static SfmExtValue FnCos(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Cos(p.Get("value").ToFloat()));
        [SfmExtFunction("math.tan")] public static SfmExtValue FnTan(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Tan(p.Get("value").ToFloat()));
        [SfmExtFunction("math.asin")] public static SfmExtValue FnAsin(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Asin(p.Get("value").ToFloat()));
        [SfmExtFunction("math.acos")] public static SfmExtValue FnAcos(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Acos(p.Get("value").ToFloat()));
        [SfmExtFunction("math.atan")] public static SfmExtValue FnAtan(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Atan(p.Get("value").ToFloat()));
        [SfmExtFunction("math.floor")] public static SfmExtValue FnFloor(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Floor(p.Get("value").ToFloat()));
        [SfmExtFunction("math.ceil")] public static SfmExtValue FnCeil(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Ceiling(p.Get("value").ToFloat()));
        [SfmExtFunction("math.sign")] public static SfmExtValue FnSign(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Sign(p.Get("value").ToFloat()));
        [SfmExtFunction("math.abs")] public static SfmExtValue FnAbs(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Abs(p.Get("value").ToFloat()));
        [SfmExtFunction("math.log")] public static SfmExtValue FnLogN(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Log(p.Get("value").ToFloat()));
        [SfmExtFunction("math.log2")] public static SfmExtValue FnLog2(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Log(p.Get("value").ToFloat(), 2));
        [SfmExtFunction("math.log10")] public static SfmExtValue FnLog10(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Log10(p.Get("value").ToFloat()));
        [SfmExtFunction("math.trunc")] public static SfmExtValue FnTrunc(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Truncate(p.Get("value").ToFloat()));
        [SfmExtFunction("math.round")] public static SfmExtValue FnRound(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Round(p.Get("value").ToFloat()));
        [SfmExtFunction("math.max")] public static SfmExtValue FnMax(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Max(p.Get("a").ToFloat(), p.Get("b").ToFloat()));
        [SfmExtFunction("math.min")] public static SfmExtValue FnMin(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Min(p.Get("a").ToFloat(), p.Get("b").ToFloat()));
        [SfmExtFunction("math.clamp")] public static SfmExtValue FnClamp(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Math.Max(p.Get("min").ToFloat(), Math.Min(p.Get("max").ToFloat(), p.Get("value").ToFloat())));
        [SfmExtFunction("math.sqrt")] public static SfmExtValue FnSqrt(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Sqrt(p.Get("value").ToFloat()));
        [SfmExtFunction("math.pow")] public static SfmExtValue FnPow(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.Pow(p.Get("base").ToFloat(), p.Get("exp").ToFloat()));
        [SfmExtFunction("math.random")] public static SfmExtValue FnRandom(SfmExtParams p, SfmExtValue u)
        {
            if (p.Has("max")) return new SfmExtValue(UnityEngine.Random.Range((float)p.Get("min").ToFloat(), (float)p.Get("max").ToFloat()));
            return new SfmExtValue(UnityEngine.Random.Range(0f, 1f));
        }
        [SfmExtFunction("math.randomint")] public static SfmExtValue FnRandomInt(SfmExtParams p, SfmExtValue u)
        {
            int min = (int)p.Get("min").ToFloat();
            int max = (int)p.Get("max").ToFloat();
            return new SfmExtValue(UnityEngine.Random.Range(min, max + 1));
        }
        [SfmExtFunction("math.pidiv")] public static SfmExtValue FnPiDiv(SfmExtParams p, SfmExtValue u) => new SfmExtValue(Math.PI / p.Get("value").ToFloat());
        [SfmExtFunction("math.deg2rad")] public static SfmExtValue FnDeg2Rad(SfmExtParams p, SfmExtValue u) => new SfmExtValue(p.Get("value").ToFloat() * Mathf.Deg2Rad);
        [SfmExtFunction("math.rad2deg")] public static SfmExtValue FnRad2Deg(SfmExtParams p, SfmExtValue u) => new SfmExtValue(p.Get("value").ToFloat() * Mathf.Rad2Deg);

        // ---------- 向量 ----------
        private static Vector3 V(SfmExtParams p) => new Vector3((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat());

        [SfmExtFunction("math.vector")] public static SfmExtValue FnVector(SfmExtParams p, SfmExtValue u)
            => VectorList(V(p));
        [SfmExtFunction("math.vector3_length")] public static SfmExtValue FnV3Len(SfmExtParams p, SfmExtValue u) => new SfmExtValue(V(p).magnitude);
        [SfmExtFunction("math.vector3_sqrlength")] public static SfmExtValue FnV3SqrLen(SfmExtParams p, SfmExtValue u) => new SfmExtValue(V(p).sqrMagnitude);
        [SfmExtFunction("math.vector3_add")] public static SfmExtValue FnV3Add(SfmExtParams p, SfmExtValue u)
            => VectorList(V(p) + new Vector3((float)p.Get("bx").ToFloat(), (float)p.Get("by").ToFloat(), (float)p.Get("bz").ToFloat()));
        [SfmExtFunction("math.vector3_sub")] public static SfmExtValue FnV3Sub(SfmExtParams p, SfmExtValue u)
            => VectorList(V(p) - new Vector3((float)p.Get("bx").ToFloat(), (float)p.Get("by").ToFloat(), (float)p.Get("bz").ToFloat()));
        [SfmExtFunction("math.vector3_scale")] public static SfmExtValue FnV3Scale(SfmExtParams p, SfmExtValue u)
            => VectorList(V(p) * (float)p.Get("s").ToFloat());
        [SfmExtFunction("math.vector3_dot")] public static SfmExtValue FnV3Dot(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Vector3.Dot(V(p), new Vector3((float)p.Get("bx").ToFloat(), (float)p.Get("by").ToFloat(), (float)p.Get("bz").ToFloat())));
        [SfmExtFunction("math.vector3_cross")] public static SfmExtValue FnV3Cross(SfmExtParams p, SfmExtValue u)
            => VectorList(Vector3.Cross(V(p), new Vector3((float)p.Get("bx").ToFloat(), (float)p.Get("by").ToFloat(), (float)p.Get("bz").ToFloat())));
        [SfmExtFunction("math.vector3_distance")] public static SfmExtValue FnV3Dist(SfmExtParams p, SfmExtValue u)
            => new SfmExtValue(Vector3.Distance(V(p), new Vector3((float)p.Get("bx").ToFloat(), (float)p.Get("by").ToFloat(), (float)p.Get("bz").ToFloat())));
        [SfmExtFunction("math.vector3_normalize")] public static SfmExtValue FnV3Norm(SfmExtParams p, SfmExtValue u)
            => VectorList(V(p).normalized);
        [SfmExtFunction("math.vector3_lerp")] public static SfmExtValue FnV3Lerp(SfmExtParams p, SfmExtValue u)
            => VectorList(Vector3.Lerp(V(p), new Vector3((float)p.Get("bx").ToFloat(), (float)p.Get("by").ToFloat(), (float)p.Get("bz").ToFloat()), (float)p.Get("t").ToFloat()));
        [SfmExtFunction("math.vector3_rotate")] public static SfmExtValue FnV3Rotate(SfmExtParams p, SfmExtValue u)
            => VectorList(Quaternion.Euler((float)p.Get("rx").ToFloat(), (float)p.Get("ry").ToFloat(), (float)p.Get("rz").ToFloat()) * V(p));
        [SfmExtFunction("math.quaternion")] public static SfmExtValue FnQuat(SfmExtParams p, SfmExtValue u)
        {
            var q = Quaternion.Euler((float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat());
            var v = new SfmExtValue(SfmExtValue.Type.List);
            v["x"] = new SfmExtValue(q.x); v["y"] = new SfmExtValue(q.y);
            v["z"] = new SfmExtValue(q.z); v["w"] = new SfmExtValue(q.w);
            return v;
        }

        private static SfmExtValue VectorList(Vector3 vec)
        {
            var v = new SfmExtValue(SfmExtValue.Type.List);
            v["x"] = new SfmExtValue(vec.x); v["y"] = new SfmExtValue(vec.y); v["z"] = new SfmExtValue(vec.z);
            return v;
        }
    }
}
