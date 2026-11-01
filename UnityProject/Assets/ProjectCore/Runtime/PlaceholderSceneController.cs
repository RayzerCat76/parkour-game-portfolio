using UnityEngine;

namespace ProjectReturn.Core
{
    public sealed class PlaceholderSceneController : MonoBehaviour
    {
        [SerializeField] private ProjectPhase phase;
        [SerializeField] private ProjectPhase nextPhase;
        [SerializeField] private string title = "Placeholder";
        [SerializeField] private bool canContinue = true;

        private bool advanceRequested;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        public ProjectPhase Phase => phase;
        public ProjectPhase NextPhase => nextPhase;
        public bool CanContinue => canContinue;
        public bool AdvanceRequested => advanceRequested;
        public string Title => title;

        public void Configure(ProjectPhase current, ProjectPhase next, string sceneTitle, bool allowContinue)
        {
            phase = current;
            nextPhase = next;
            title = sceneTitle;
            canContinue = allowContinue;
        }

        private void Update()
        {
            if (canContinue && ProjectInput.EnterPressedThisFrame())
                Continue();
        }

        public bool Continue()
        {
            if (!canContinue || advanceRequested)
                return false;

            ProjectServicesRoot services = ProjectServicesRoot.EnsureExists();
            if (!services.GameFlow.RequestPhase(nextPhase))
                return false;

            advanceRequested = true;
            return true;
        }

        private void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.78f, 0.84f, 0.92f) }
            };

            float width = Mathf.Min(720f, Screen.width - 40f);
            Rect area = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.5f - 115f, width, 230f);
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 20f, area.y + 25f, area.width - 40f, 60f), title, titleStyle);
            GUI.Label(new Rect(area.x + 20f, area.y + 95f, area.width - 40f, 36f), "Placeholder scene", bodyStyle);
            string prompt = canContinue ? "Press Enter to continue" : "End of current scaffold";
            GUI.Label(new Rect(area.x + 20f, area.y + 150f, area.width - 40f, 36f), prompt, bodyStyle);
        }
    }
}
