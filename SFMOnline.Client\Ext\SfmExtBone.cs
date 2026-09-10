using System;
using System.Collections.Generic;
using UnityEngine;

namespace SFMOnline.Ext
{
    // ====================================================================
    //  骨骼扩展系统：
    //  - 读取/设置本地玩家任意骨骼的位置/旋转/缩放
    //  - 查找任意远端玩家（Ghost）的骨骼
    //  - 附加骨骼（Attach）：把任意 GameObject 绑定到指定骨骼跟随移动
    //  - 扩展骨骼同步：本地自定义骨骼数据可广播给房间（ext_bone）
    //  - 模仿 V2 的骨骼读取 + 我们模组的 CoreBoneNames/FindLocalBone
    // ====================================================================
    public static class SfmExtBone
    {
        // 玩家引用类型：Local=自己, Ghost=远端玩家(uid/peerId)
        public enum PlayerTarget { Local, Ghost }

        private sealed class AttachEntry
        {
            public Transform Attached;
            public string BoneName;
            public Vector3 Offset;
            public bool SyncRot;
            public string OwnerUid; // 为空=本地附加
        }

        private static readonly List<AttachEntry> _attachments = new List<AttachEntry>();

        // ---------- 本地骨骼 ----------
        /// <summary>按名字查找本地玩家骨骼（任意深度）。</summary>
        public static Transform FindLocalBone(string boneName)
        {
            try
            {
                var core = OnlineCore.Instance;
                if (core == null) return null;
                return core.FindLocalBonePublic(boneName);
            }
            catch { return null; }
        }

        /// <summary>按名字查找远端玩家（Ghost）骨骼。</summary>
        public static Transform FindGhostBone(string uid, string boneName)
        {
            try
            {
                var ghost = GetGhostRoot(uid);
                if (ghost == null) return null;
                return FindBoneIn(ghost, boneName);
            }
            catch { return null; }
        }

        /// <summary>获取远端玩家根 GameObject。</summary>
        public static GameObject GetGhostRoot(string uid)
        {
            try
            {
                var core = OnlineCore.Instance;
                if (core == null) return null;
                return core.GetGhostRootByUid(uid);
            }
            catch { return null; }
        }

        /// <summary>DFS 查找骨骼（模仿 OnlineCore.FindBoneIn）。</summary>
        public static Transform FindBoneIn(GameObject root, string boneName)
        {
            if (root == null) return null;
            return FindBoneIn(root.transform, boneName);
        }

        public static Transform FindBoneIn(Transform root, string boneName)
        {
            if (root == null) return null;
            if (root.name == boneName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindBoneIn(root.GetChild(i), boneName);
                if (r != null) return r;
            }
            return null;
        }

        // ---------- 读取 ----------
        public static Vector3 GetLocalBonePosition(string boneName)
        {
            var t = FindLocalBone(boneName);
            return t != null ? t.position : Vector3.zero;
        }

        public static Quaternion GetLocalBoneRotation(string boneName)
        {
            var t = FindLocalBone(boneName);
            return t != null ? t.rotation : Quaternion.identity;
        }

        public static Vector3 GetLocalBoneScale(string boneName)
        {
            var t = FindLocalBone(boneName);
            return t != null ? t.lossyScale : Vector3.one;
        }

        // ---------- 设置 ----------
        public static void SetLocalBonePosition(string boneName, Vector3 position)
        {
            var t = FindLocalBone(boneName);
            if (t != null) t.position = position;
        }

        public static void SetLocalBoneRotation(string boneName, Quaternion rotation)
        {
            var t = FindLocalBone(boneName);
            if (t != null) t.rotation = rotation;
        }

        public static void SetLocalBoneScale(string boneName, Vector3 scale)
        {
            var t = FindLocalBone(boneName);
            if (t != null) t.localScale = scale;
        }

        /// <summary>设置本地骨骼旋转并广播给房间（其它玩家同步该骨骼）。</summary>
        public static void SetLocalBoneRotationSynced(string boneName, Quaternion rotation)
        {
            SetLocalBoneRotation(boneName, rotation);
            SfmExtMsg.SendToRoom(new Dictionary<string, object>
            {
                ["t"] = "ext_bone", ["ns"] = SfmExtEventBus.Namespace,
                ["op"] = "rot", ["bone"] = boneName,
                ["x"] = rotation.x, ["y"] = rotation.y, ["z"] = rotation.z, ["w"] = rotation.w
            });
        }

        // ---------- 附加骨骼 ----------
        /// <summary>把 GameObject 附加到本地骨骼，跟随移动/旋转。</summary>
        public static bool AttachToBone(GameObject obj, string boneName, Vector3? offset = null, bool syncRot = true)
        {
            var t = FindLocalBone(boneName);
            if (t == null || obj == null) return false;
            _attachments.RemoveAll(a => a.Attached == obj.transform);
            _attachments.Add(new AttachEntry
            {
                Attached = obj.transform,
                BoneName = boneName,
                Offset = offset ?? Vector3.zero,
                SyncRot = syncRot,
                OwnerUid = null
            });
            UpdateAttachment(_attachments[_attachments.Count - 1], t);
            return true;
        }

        public static void Detach(GameObject obj)
        {
            _attachments.RemoveAll(a => a.Attached == obj.transform);
        }

        // ---------- 引擎函数 ----------
        [SfmExtFunction("bone.find")]
        public static SfmExtValue FnFind(SfmExtParams p, SfmExtValue unused)
        {
            var uid = p.Get("uid").ToString();
            var bone = p.Get("bone").ToString();
            if (string.IsNullOrEmpty(uid))
                return new SfmExtValue(FindLocalBone(bone) != null);
            return new SfmExtValue(FindGhostBone(uid, bone) != null);
        }

        [SfmExtFunction("bone.setrot")]
        public static SfmExtValue FnSetRot(SfmExtParams p, SfmExtValue unused)
        {
            var bone = p.Get("bone").ToString();
            var t = FindLocalBone(bone);
            if (t == null) return new SfmExtValue(false);
            t.localRotation = Quaternion.Euler(
                (float)p.Get("x").ToFloat(), (float)p.Get("y").ToFloat(), (float)p.Get("z").ToFloat());
            return new SfmExtValue(true);
        }

        [SfmExtFunction("bone.getrot")]
        public static SfmExtValue FnGetRot(SfmExtParams p, SfmExtValue unused)
        {
            var t = FindLocalBone(p.Get("bone").ToString());
            var e = t != null ? t.localRotation.eulerAngles : Vector3.zero;
            var v = new SfmExtValue(SfmExtValue.Type.List)
            {
                ["x"] = new SfmExtValue(e.x), ["y"] = new SfmExtValue(e.y), ["z"] = new SfmExtValue(e.z)
            };
            return v;
        }

        // ---------- 内部 ----------
        [SfmExtUpdate]
        public static void Update()
        {
            for (int i = 0; i < _attachments.Count; i++)
            {
                var a = _attachments[i];
                var t = a.OwnerUid == null ? FindLocalBone(a.BoneName) : null;
                if (t == null) continue;
                UpdateAttachment(a, t);
            }
        }

        private static void UpdateAttachment(AttachEntry a, Transform bone)
        {
            if (a.Attached == null) return;
            a.Attached.position = bone.position + bone.rotation * a.Offset;
            if (a.SyncRot) a.Attached.rotation = bone.rotation;
        }
    }
}
