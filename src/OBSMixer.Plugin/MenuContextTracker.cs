using System;
using System.Linq;
using UnityEngine;

namespace OBSMixer
{
    internal sealed class MenuContextTracker
    {
        private LevelSelectionNavigationController[] controllers = Array.Empty<LevelSelectionNavigationController>();
        private float nextDiscovery;
        internal void Reset() { nextDiscovery = 0; }
        internal bool? IsSelecting()
        {
            if (Time.unscaledTime >= nextDiscovery)
            {
                nextDiscovery = Time.unscaledTime + 2;
                controllers = Resources.FindObjectsOfTypeAll<LevelSelectionNavigationController>();
            }
            // Preserve the current pose during navigation animations.
            if (controllers.Any(c => c && c.isInTransition)) return null;
            return controllers.Any(c => c && c.isActiveAndEnabled && c.isInViewControllerHierarchy);
        }
    }
}
