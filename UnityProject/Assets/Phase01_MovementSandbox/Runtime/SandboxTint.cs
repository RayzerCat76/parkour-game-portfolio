using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class SandboxTint : MonoBehaviour
    {
        [SerializeField] private Color color = Color.white;

        public void Configure(Color value) => color = value;

        private void Awake()
        {
            Renderer target = GetComponent<Renderer>();
            if (target == null)
                return;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            target.SetPropertyBlock(block);
        }
    }
}
