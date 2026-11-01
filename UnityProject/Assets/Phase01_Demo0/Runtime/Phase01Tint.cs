using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01Tint : MonoBehaviour
    {
        [SerializeField] private float red = 1f;
        [SerializeField] private float green = 1f;
        [SerializeField] private float blue = 1f;
        [SerializeField] private float alpha = 1f;

        public void Configure(Color tint)
        {
            red = tint.r;
            green = tint.g;
            blue = tint.b;
            alpha = tint.a;
        }

        private void Awake()
        {
            Renderer targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null)
                return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(block);
            Color tint = new Color(red, green, blue, alpha);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            targetRenderer.SetPropertyBlock(block);
        }
    }
}
