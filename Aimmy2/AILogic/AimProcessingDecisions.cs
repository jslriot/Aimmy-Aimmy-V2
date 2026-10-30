namespace Aimmy2.AILogic
{
    internal static class AimProcessingDecisions
    {
        internal static bool ShouldRunPrediction(
            bool showDetectedPlayer,
            bool constantAiTracking,
            bool aimKeyHeld,
            bool secondAimKeyHeld) =>
            showDetectedPlayer ||
            constantAiTracking ||
            aimKeyHeld ||
            secondAimKeyHeld;

        internal static bool ShouldProcessFrame(
            bool aimAssist,
            bool showDetectedPlayer,
            bool autoTrigger) =>
            aimAssist ||
            showDetectedPlayer ||
            autoTrigger;

        // autoTriggerKeybindHeld lets the Auto Trigger Keybind fire on its own,
        // without also requiring an Aim Keybind held or Constant AI Tracking
        // off - that's the whole point of a dedicated hold-to-fire keybind.
        // The permanent Auto Trigger toggle still goes through the normal
        // aim-key + not-constant-tracking path, unchanged.
        internal static bool ShouldAttemptAutoTrigger(
            bool autoTrigger,
            bool aimKeyHeld,
            bool secondAimKeyHeld,
            bool constantAiTracking,
            bool autoTriggerKeybindHeld) =>
            autoTrigger &&
            (autoTriggerKeybindHeld || ((aimKeyHeld || secondAimKeyHeld) && !constantAiTracking));

        internal static bool ShouldKeepSprayActive(
            bool autoTrigger,
            bool aimKeyHeld,
            bool secondAimKeyHeld,
            bool autoTriggerKeybindHeld) =>
            autoTrigger &&
            (autoTriggerKeybindHeld || aimKeyHeld || secondAimKeyHeld);
    }
}
