using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime.Injection;
#endif

namespace S1MAPI.Building
{
    /// <summary>
    /// Thin MonoBehaviour shell that forwards Unity lifecycle calls to
    /// <see cref="InteriorNavigatorCore"/>. Kept minimal so IL2CPP's
    /// ClassInjector only sees simple Unity-compatible method signatures
    /// (no custom parameter types → zero registration warnings).
    /// </summary>
    internal sealed class InteriorNavigator : MonoBehaviour
    {
        /// <summary>Navigation logic core, forwarded from Unity lifecycle methods.</summary>
        internal InteriorNavigatorCore? _core;

        private void Update()
        {
            _core?.Update();
        }

        private void OnDestroy()
        {
            _core?.Cleanup();
            _core = null;
        }
    }
}
