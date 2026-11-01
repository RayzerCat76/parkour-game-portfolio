using System;
using UnityEngine.InputSystem;

namespace ProjectReturn.Core
{
    public enum ProjectPhase
    {
        Boot,
        Prologue,
        Phase01,
        Phase02,
        Phase03,
        Phase04,
        Ending
    }

    public static class ProjectSceneNames
    {
        public const string Boot = "Boot";
        public const string Prologue = "Prologue";
        public const string Phase01 = "Phase01_Demo0";
        public const string Phase02 = "Phase02";
        public const string Phase03 = "Phase03";
        public const string Phase04 = "Phase04";
        public const string Ending = "Ending";
        public const string MovementSandbox = "Phase01_MovementSandbox";

        public static readonly ProjectPhase[] OrderedPhases =
        {
            ProjectPhase.Boot,
            ProjectPhase.Prologue,
            ProjectPhase.Phase01,
            ProjectPhase.Phase02,
            ProjectPhase.Phase03,
            ProjectPhase.Phase04,
            ProjectPhase.Ending
        };

        public static string ForPhase(ProjectPhase phase)
        {
            return phase switch
            {
                ProjectPhase.Boot => Boot,
                ProjectPhase.Prologue => Prologue,
                ProjectPhase.Phase01 => Phase01,
                ProjectPhase.Phase02 => Phase02,
                ProjectPhase.Phase03 => Phase03,
                ProjectPhase.Phase04 => Phase04,
                ProjectPhase.Ending => Ending,
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
            };
        }

        public static bool TryGetPhase(string sceneName, out ProjectPhase phase)
        {
            foreach (ProjectPhase candidate in OrderedPhases)
            {
                if (string.Equals(ForPhase(candidate), sceneName, StringComparison.Ordinal))
                {
                    phase = candidate;
                    return true;
                }
            }

            phase = ProjectPhase.Boot;
            return false;
        }

        public static bool TryGetNext(ProjectPhase phase, out ProjectPhase next)
        {
            int index = Array.IndexOf(OrderedPhases, phase);
            if (index < 0 || index >= OrderedPhases.Length - 1)
            {
                next = phase;
                return false;
            }

            next = OrderedPhases[index + 1];
            return true;
        }
    }

    internal static class ProjectInput
    {
        public static bool EnterPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }

        public static bool ShiftIsPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        public static int PressedDevelopmentDigit()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return 0;
            if (keyboard.digit1Key.wasPressedThisFrame) return 1;
            if (keyboard.digit2Key.wasPressedThisFrame) return 2;
            if (keyboard.digit3Key.wasPressedThisFrame) return 3;
            if (keyboard.digit4Key.wasPressedThisFrame) return 4;
            if (keyboard.digit5Key.wasPressedThisFrame) return 5;
            if (keyboard.digit6Key.wasPressedThisFrame) return 6;
            return 0;
        }
    }
}
