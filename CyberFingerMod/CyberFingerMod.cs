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
	//internal const string VERSION_CONSTANT = "0.0.2"; //Changing the version here updates it in all locations needed
	public override string Name => "CyberFingerMod";
	public override string Author => "Dr.Sci.Cortex";

	public override string Link => "https://github.com/DrSciCortex/CyberFingerMod/";

	public override void OnEngineInit() {
		Harmony harmony = new Harmony("com.example.CyberFingerMod");
		harmony.PatchAll();
	}

	// Cache at load
	private static readonly string _version = ResolveVersion();
	public override string Version => _version;

	private static string ResolveVersion() {
		var asm = typeof(CyberFingerMod).Assembly;

		// 1) FileVersion
		var file = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
		if (!string.IsNullOrWhiteSpace(file)) return file;

		// 2) InformationalVersion
		var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(info)) return info;

		// 3) AssemblyVersion
		var av = asm.GetName().Version?.ToString();
		if (!string.IsNullOrWhiteSpace(av)) return av;

		return "X.Y.Z-failed-to-find-version";
	}

	//Implement the HarmonyPatch
	[HarmonyPatch(typeof(InteractionHandler), "UpdateUserspaceToolOffsets")]
	public static class InteractionHandler_UpdateUserspaceToolOffsets_Patch {

		// reflect the protected _laserSlot field
		private static readonly FieldInfo LaserSlotField =
			typeof(InteractionHandler).GetField("_laserSlot", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException("InteractionHandler._laserSlot not found");

		// Cache the DoRaycast method from Laser
		private static readonly MethodInfo DoRaycastMethod =
			typeof(Laser).GetMethod("DoRaycast",
				BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("Laser.DoRaycast not found");

		static void Postfix(InteractionHandler __instance) {
			// only override for userspace
			if (__instance.World != Userspace.UserspaceWorld)
				return;

			return; 

			// copy properties to locals before using `in`
			float3 tip = __instance.CurrentTip;
			float3 fwd = __instance.CurrentTipForward;


			// Compute the "world-style" tip/forward
			float3 localTip = __instance.Slot.GlobalPointToLocal(in tip);
			float3 localForward = __instance.Slot.GlobalDirectionToLocal(in fwd);

			floatQ localRotation = floatQ.LookRotation(in localForward, float3.Up);

			var laserSlot = (Slot)LaserSlotField.GetValue(__instance)!;

			laserSlot.LocalPosition = __instance.Slot.LocalPointToSpace(in localTip, laserSlot.Parent);
			laserSlot.LocalRotation = __instance.Slot.LocalRotationToSpace(in localRotation, laserSlot.Parent);

			// Re-set active state against the CURRENT hit (raycast comes from Laser.Update)
			Userspace.SetUserspaceLaserActive(
				__instance.Side,
				__instance.Laser.LaserActive,
				__instance.Laser.CurrentHit != null
			);

		}


	}
}
