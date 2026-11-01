using System.Collections;
using System.Collections.Generic;
using Phase01.MovementSandbox;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase02.VolcanoEscape
{
    public sealed class Phase02AcceptanceDriver : MonoBehaviour
    {
        private static readonly float[] ShockwaveDesignPositions = { 400f, 540f, 620f, 905f };
        private static readonly int[] ShockwaveSections = { 1, 2, 2, 3 };

        private Phase02VolcanoEscapeController controller;
        private Phase01ParkourMotor motor;
        private Coroutine runRoutine;
        private GUIStyle buttonStyle;

        public bool IsConfigured => controller != null && motor != null;
        public bool IsRunning => runRoutine != null;
        public bool Passed { get; private set; }
        public string Report { get; private set; } = "Not run";

        public void Configure(Phase02VolcanoEscapeController owner, Phase01ParkourMotor playerMotor)
        {
            controller = owner;
            motor = playerMotor;
        }

        public bool BeginAutomatedRun()
        {
            if (!IsConfigured || IsRunning || !controller.IsPlayable)
                return false;

            Passed = false;
            Report = "Running";
            runRoutine = StartCoroutine(Run());
            return true;
        }

        private IEnumerator Run()
        {
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + 360f;
            float releaseJumpAt = -1f;
            float lastJumpAt = -10f;

            while (SceneManager.GetActiveScene().name == "Phase02"
                && controller != null
                && Time.realtimeSinceStartup < deadline)
            {
                if (controller.CompletionRequested)
                {
                    Passed = true;
                    Report = $"Passed in {Time.realtimeSinceStartup - startedAt:F1}s with {controller.TotalFailures} local resets.";
                    break;
                }

                if (controller.IsResetting || !controller.IsPlayable)
                {
                    motor.SetSimulatedInput(0f, false);
                    yield return null;
                    continue;
                }

                float now = Time.realtimeSinceStartup;
                float x = controller.PlayerX;
                bool holdJump = releaseJumpAt > now;
                if (!holdJump && releaseJumpAt >= 0f)
                {
                    releaseJumpAt = -1f;
                    motor.SimulateJumpRelease();
                }

                bool shouldJump = motor.IsGrounded
                    && now - lastJumpAt >= 0.3f
                    && (ApproachingRequiredGap(x, controller.GapIntervals)
                        || ApproachingShockwave(x)
                        || x >= controller.RouteLength - 22f);
                if (shouldJump)
                {
                    motor.SimulateJumpPress(true);
                    releaseJumpAt = now + 0.34f;
                    lastJumpAt = now;
                    holdJump = true;
                }

                motor.SetSimulatedInput(1f, holdJump);
                yield return null;
            }

            motor?.ClearSimulatedInput();
            if (!Passed)
            {
                float stoppedX = controller != null ? controller.PlayerX : -1f;
                Report = $"Stopped after {Time.realtimeSinceStartup - startedAt:F1}s at x={stoppedX:F1}.";
            }
            runRoutine = null;
        }

        private static bool ApproachingRequiredGap(float x, IReadOnlyList<Vector2> gaps)
        {
            for (int i = 0; i < gaps.Count; i++)
            {
                Vector2 gap = gaps[i];
                if (gap.y < x - 0.2f)
                    continue;
                return gap.x - x >= 0.72f && gap.x - x <= 1.2f;
            }
            return false;
        }

        private static bool ApproachingShockwave(float x)
        {
            for (int i = 0; i < ShockwaveDesignPositions.Length; i++)
            {
                float authoredX = Phase02VolcanoEscapeController.MapDesignX(
                    ShockwaveSections[i],
                    ShockwaveDesignPositions[i]);
                float distance = authoredX - 12f - x;
                if (distance >= -0.25f && distance <= 1.1f)
                    return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if ((!Application.isEditor && !Debug.isDebugBuild) || !IsConfigured || IsRunning)
                return;

            buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            if (GUI.Button(new Rect(15f, Screen.height - 52f, 220f, 34f), "Run Phase 02 acceptance", buttonStyle))
                BeginAutomatedRun();
        }
    }
}
