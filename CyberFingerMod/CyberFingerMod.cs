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
	internal const string VERSION_CONSTANT = "0.0.2"; //Changing the version here updates it in all locations needed
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
			.GetMethod("EndGrab", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("InteractionHandler.EndGrab not found");

		// reflect the protected _laserSlot field
		private static readonly FieldInfo LaserSlotField =
			typeof(InteractionHandler).GetField("_laserSlot", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException("InteractionHandler._laserSlot not found");


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

			//float3 globalPoint = __instance.CurrentTip;
			//float3 globalDirection = __instance.CurrentTipForward;

			//float3 offset = __instance.Slot.GlobalPointToLocal(in globalPoint);
			//float3 forward = __instance.Slot.GlobalDirectionToLocal(in globalDirection);
			//Userspace.SetWorldControllerData(__instance.Side, __instance.ActiveTool?.IsInUse ?? false, offset, forward, __instance.Laser.Slot.LocalScaleToSpace(__instance.Laser.CurrentPointDistance, __instance.LocalUserRoot.Slot));

			float3 a = controllerData.pointOffset;
			float3 a2 = controllerData.forward;

			floatQ localRotation = floatQ.LookRotation(in a2, float3.Up);
			//_laserSlot.Target.LocalPosition = base.Slot.LocalPointToSpace(in a, _laserSlot.Target.Parent);
			//_laserSlot.Target.LocalRotation = base.Slot.LocalRotationToSpace(in localRotation, _laserSlot.Target.Parent);

			// now fetch _laserSlot via reflection

			// old 4.7.2
			// it's a SyncRef<Slot>, so first unbox it:
			//#var laserSlotRef = (SyncRef<Slot>)LaserSlotField.GetValue(__instance);
			// then its Target is the actual Slot
			//#Slot laserSlot = laserSlotRef.Target;

			// new 9.0
			var laserSlot = (Slot)LaserSlotField.GetValue(__instance)!;

			laserSlot.LocalPosition = __instance.Slot.LocalPointToSpace(in a, laserSlot.Parent);
			laserSlot.LocalRotation = __instance.Slot.LocalRotationToSpace(in localRotation, laserSlot.Parent);

			Userspace.SetUserspaceLaserActive(__instance.Side, __instance.Laser.LaserActive, __instance.Laser.CurrentHit != null);


			return false;

		}
	}
}
