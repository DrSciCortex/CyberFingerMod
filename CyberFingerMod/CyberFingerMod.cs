using System;
using System.Reflection;

using Elements.Core;

using FrooxEngine;

using HarmonyLib;

using Renderite.Shared;

using ResoniteModLoader;

using static Elements.Core.Pool;

namespace CyberFingerMod;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class CyberFingerMod : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.1"; //Changing the version here updates it in all locations needed
	public override string Name => "CyberFingerMod";
	public override string Author => "DrSciCortex";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DrSciCortex/CyberFingerMod/";

	// RML config handles UI + persistence for you
	private static ModConfiguration? _config;

	// The toggle you'll see in the UI (default: true)
	internal static readonly ModConfigurationKey<bool> ForceOverrideKey =
		new ModConfigurationKey<bool>(
			"ForceOverride",
			"Force userspace laser override (skip engine’s userspace branch and use mod logic).",
			() => true // default value
		);

	// Helper so patch code can read the toggle safely
	internal static bool ForceOverride =>
		_config?.GetValue(ForceOverrideKey) ?? true;

	public override void OnEngineInit() {

		_config = GetConfiguration();  // creates/loads config and UI
		_config?.Save(true);

		Harmony harmony = new Harmony("com.scicortex.CyberFingerMod");
		harmony.PatchAll();
	}

	//Implement the HarmonyPatch
	[HarmonyPatch(typeof(InteractionHandler), "UpdateUserspaceToolOffsets")]
	public static class InteractionHandler_UpdateUserspaceToolOffsets_Patch {

		// --- Cache reflection once ---
		private static readonly MethodInfo EndGrabMethod =
			typeof(InteractionHandler).GetMethod("EndGrab", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("InteractionHandler.EndGrab not found");

		// reflect the protected _laserSlot field
		private static readonly FieldInfo LaserSlotField =
			typeof(InteractionHandler).GetField("_laserSlot", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException("InteractionHandler._laserSlot not found");

		static bool Prefix(InteractionHandler __instance) {

			// Let original handle non-userspace or when toggle is off
			bool userspace = __instance.World == Userspace.UserspaceWorld;
			if (!userspace || !CyberFingerMod.ForceOverride)
				return true;

			Userspace.ControllerData controllerData = Userspace.GetControllerData(__instance.Side);

			controllerData.userspaceController = __instance;
			controllerData.userspaceHoldingThings = __instance.IsHoldingObjects;
			if (controllerData.worldHoldingThings) {
				// invoke the private EndGrab()
				EndGrabMethod.Invoke(__instance, null);
			}

			float3 localTip2 = __instance.Slot.GlobalPointToLocal(__instance.RawCurrentTip);
			float3 localForward2 = __instance.Slot.GlobalDirectionToLocal(__instance.RawCurrentTipForward);
			float3 targetLocalTip = controllerData.pointOffset;
			float3 targetLocalForward = controllerData.forward;
			float num2 = MathX.Distance(in targetLocalTip, in localTip2);
			float angle = MathX.Angle(in targetLocalForward, in localForward2);
			if (!CyberFingerMod.ForceOverride && (num2 > 0.3f || angle > 45f)) {
				targetLocalTip = localTip2;
				targetLocalForward = localForward2;
			}

			floatQ localRotation = floatQ.LookRotation(in targetLocalForward, float3.Up);

			// now fetch _laserSlot via reflection
			// it's a SyncRef<Slot>, so first unbox it:
			var laserSlotRef = (SyncRef<Slot>)LaserSlotField.GetValue(__instance)!;

			// then its Target is the actual Slot
			Slot laserSlot = laserSlotRef.Target;
			laserSlot.LocalPosition = __instance.Slot.LocalPointToSpace(in targetLocalTip, laserSlot.Parent);
			laserSlot.LocalRotation = __instance.Slot.LocalRotationToSpace(in localRotation, laserSlot.Parent);
			Userspace.SetUserspaceLaserActive(__instance.Side, __instance.Laser.LaserActive, __instance.Laser.CurrentHit != null);

			// Don't let the original method run. 
			return false;

		}


	}
}
