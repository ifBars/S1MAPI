using UnityEngine;
using Object = UnityEngine.Object;

namespace S1MAPI.Extensions
{
    /// <summary>
    /// Extension methods for Transform operations.
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Reset transform to identity (position=zero, rotation=identity, scale=one).
        /// </summary>
        public static Transform Reset(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            return transform;
        }

        /// <summary>
        /// Set local position with optional axis parameters.
        /// </summary>
        public static Transform SetLocalPosition(this Transform transform, float? x = null, float? y = null, float? z = null)
        {
            Vector3 pos = transform.localPosition;
            if (x.HasValue) pos.x = x.Value;
            if (y.HasValue) pos.y = y.Value;
            if (z.HasValue) pos.z = z.Value;
            transform.localPosition = pos;
            return transform;
        }

        /// <summary>
        /// Destroy all child GameObjects of this transform.
        /// </summary>
        public static void DestroyChildren(this Transform transform)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Get all children as a GameObject array.
        /// </summary>
        public static GameObject[] GetChildren(this Transform transform)
        {
            GameObject[] children = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                children[i] = transform.GetChild(i).gameObject;
            }
            return children;
        }
    }
}
