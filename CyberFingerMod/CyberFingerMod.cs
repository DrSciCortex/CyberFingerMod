using System.Reflection;

using Elements.Core;

using FrooxEngine;

using HarmonyLib;

using ResoniteModLoader;

using static Elements.Core.Pool;

namespace CyberFingerMod;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class CyberFingerMod : ResoniteMod {
	internal const string VERSION_CONSTANT = "0.0.1"; //Changing the version here updates it in all locations needed
	public override string Name => "CyberFingerMod";
	public override string Author => "Dr.Sci.Cortex";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DrSciCortex/CyberFingerMod/";

	public override void OnEngineInit() {
		Harmony harmony = new Harmony("com.example.CyberFingerMod");
		harmony.PatchAll();
	}

	//Implement the HarmonyPatch
	[HarmonyPatch(typeof(InteractionHandler), "UpdateUserspaceToolOffsets")]
	public static class InteractionHandler_UpdateUserspaceToolOffsets_Patch {

		// reflect the private EndGrab method once:
		private static readonly MethodInfo EndGrabMethod = typeof(InteractionHandler)
			.GetMethod("EndGrab", BindingFlags.Instance | BindingFlags.NonPublic);

		static bool Prefix(InteractionHandler __instance) {
			//Msg("Postfix from CyberFingerMod");

			bool userspace = __instance.World == Userspace.UserspaceWorld;
			Userspace.ControllerData controllerData = Userspace.GetControllerData(__instance.Side);
			
			// OK to run the original method
			if (!userspace) 
				return true;

			// Otherwise ... a revised method that doesn't override the laser position, but is otherwise equivalent.
			controllerData.userspaceController = __instance;
			controllerData.userspaceHoldingThings = __instance.IsHoldingObjects;
			if (controllerData.worldHoldingThings) {
				// invoke the private EndGrab()
				EndGrabMethod.Invoke(__instance, null);
			}

			float3 globalPoint = __instance.CurrentTip;
			float3 globalDirection = __instance.CurrentTipForward;

			float3 offset = __instance.Slot.GlobalPointToLocal(in globalPoint);
			float3 forward = __instance.Slot.GlobalDirectionToLocal(in globalDirection);
			Userspace.SetWorldControllerData(__instance.Side, __instance.ActiveTool?.IsInUse ?? false, offset, forward, __instance.Laser.Slot.LocalScaleToSpace(__instance.Laser.CurrentPointDistance, __instance.LocalUserRoot.Slot));

			Userspace.SetUserspaceLaserActive(__instance.Side, __instance.Laser.LaserActive, __instance.Laser.CurrentHit != null);

			return false;

		}
	}
}
