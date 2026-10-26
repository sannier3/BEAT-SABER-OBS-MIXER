using CP_SDK.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OBSMixer
{
    internal static class CurvedGraphics
    {
        public static RectTransform Container(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UISystem.UILayer;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            return rect;
        }
        public static Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            var component = UISystem.ImageFactory.Create(name, parent);
            component.LElement.ignoreLayout = true;
            var image = component.ImageC;
            image.color = color; image.raycastTarget = raycast; image.preserveAspect = false;
            return image;
        }
    }
}
