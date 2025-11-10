using System;
using System.Collections;          // <-- this gives you IList
using System.Collections.Generic;
using System.Reflection;

using Elements.Core;

using FrooxEngine;
using FrooxEngine.CommonAvatar;

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
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> ForceOverrideKey =
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

		//Engine.Current.RunPostInit(SetupBindings);
		Msg("Initalized CyberFingerMod");
		// Debug(...); Error(...); 
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


	//Implement the HarmonyPatch
	[HarmonyPatch(typeof(StandardGamepad), "Bind")]
	public static class StandardGamepad_Bind_Patch {

		static private ScreenLocomotionDirection GenerateScreenDirection(StandardGamepad __instance) {
			ScreenLocomotionDirection screenLocomotionDirection = new ScreenLocomotionDirection();
			screenLocomotionDirection.Axis = InputNode.Analog2D(__instance.LeftThumbstick);
			screenLocomotionDirection.Up = InputNode.Digital(__instance.RightThumbstickClick);
			screenLocomotionDirection.Down = InputNode.Digital(__instance.LeftThumbstickClick);
			return screenLocomotionDirection;
		}

		static private VR_SingleLocomotionTurn GenerateTurn(StandardGamepad __instance) {
			return new VR_SingleLocomotionTurn(InputNode.Analog2D(__instance.RightThumbstick).X());
		}


		static bool Prefix(StandardGamepad __instance, InputGroup group) {

			/*
			if (!(group is GlobalActions globalActions)) {
				if (!(group is InteractionHandlerInputs commonTool)) {
					if (!(group is ContextMenuInputs contextMenu)) {
						if (!(group is LaserHoldInputs laserHold)) {
							if (!(group is ScreenCameraInputs screenCamera)) {
								if (!(group is HeadInputs headInputs)) {
									if (!(group is TeleportInputs teleport)) {
										if (!(group is SmoothLocomotionInputs smoothLocomotion)) {
											if (!(group is SmoothThreeAxisLocomotionInputs threeAxisLocomotion)) {
												if (!(group is AnchorLocomotionInputs anchorInputs)) {
													if (group is AnchorReleaseInputs anchorRelease) {
														anchorRelease.Release.AddBinding(__instance.A);
														anchorRelease.ReleaseStrength.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick).Magnitude(), __instance);
														return false;
													}
													UniLog.Warning($"Cannot bind {group} to Standard Gamepad ({((InputDevice)__instance).Name})");
												} else {
													anchorInputs.PrimaryAxis.AddBinding(__instance.LeftThumbstick);
													anchorInputs.SecondaryAxis.AddBinding(__instance.RightThumbstick);
													anchorInputs.PrimaryAction.AddBinding(__instance.LeftThumbstickClick);
													anchorInputs.SecondaryAction.AddBinding(__instance.RightThumbstickClick);
												}
											} else {
												threeAxisLocomotion.Move.AddBinding(GenerateScreenDirection(__instance), __instance);
												threeAxisLocomotion.Jump.AddBinding(__instance.LeftThumbstickClick);
												threeAxisLocomotion.Align.AddBinding(__instance.B);
											}
										} else {
											smoothLocomotion.Move.AddBinding(GenerateScreenDirection(__instance), __instance);
											smoothLocomotion.Jump.AddBinding(__instance.LeftThumbstickClick);
											smoothLocomotion.TurnDelta.AddBinding(GenerateTurn(__instance), __instance);
										}
									} else {
										teleport.Teleport.AddBinding(__instance.LeftThumbstickClick);
										teleport.Backstep.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick).Y().Negate()
											.ToDigital(0.8f), __instance);
										teleport.TurnDelta.AddBinding(GenerateTurn(__instance), __instance);
									}
								} else {
									headInputs.Crouch.AddBinding(InputNode.Digital(__instance.RightThumbstickClick).ToAnalog(), __instance);
									headInputs.Crouch.AddBinding(InputNode.Digital(__instance.RightThumbstickClick).MultiTapToggle().ToAnalog(), __instance);
								}
							} else {
								//screenCamera.Look.AddBinding(InputNode.Analog2D(RightThumbstick).Pow(InputNode.Setting((GamepadSettings s) => s.ThumbstickLookExponent, 0f)).Multiply(InputNode.Setting((GamepadSettings s) => s.ThumbstickLookSpeed, 0f)), this);
							}
						} else {
							laserHold.Align.AddBinding(InputNode.PrimarySecondary(InputNode.Digital(__instance.RightThumbstickClick), null), __instance);
							laserHold.Slide.AddBinding(InputNode.PrimarySecondary(InputNode.Analog2D(__instance.RightThumbstick).Y(), null), __instance);
							laserHold.Rotate.AddBinding(InputNode.PrimarySecondary(InputNode.Analog2D(__instance.RightThumbstick).X(), null), __instance);
						}
					} else {
						contextMenu.SelectDirection.AddBinding(__instance.RightThumbstick);
						contextMenu.Select.AddBinding(InputNode.Digital(Chirality.Right, __instance.A.Name), __instance);
						contextMenu.Select.AddBinding(InputNode.Digital(Chirality.Left, __instance.X.Name), __instance);
					}
				} else {
					commonTool.Interact.AddBinding(InputNode.Digital(Chirality.Right, __instance.A.Name), __instance);
					commonTool.Interact.AddBinding(InputNode.Digital(Chirality.Left, __instance.X.Name), __instance);
					//commonTool.Interact.AddBinding(InputNode.PrimarySecondary(InputNode.Digital(__instance.X), null), __instance);
					//commonTool.Strength.AddBinding(InputNode.PrimarySecondary(InputNode.Analog(RightTrigger), null), this);
					//commonTool.Grab.AddBinding(InputNode.PrimarySecondary(InputNode.Digital(__instance.B), null), __instance);
					//commonTool.Grab.AddBinding(InputNode.PrimarySecondary(InputNode.Digital(__instance.Y), null), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(Chirality.Right, __instance.B.Name), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(Chirality.Left, __instance.Y.Name), __instance);
					commonTool.Menu.AddBinding(InputNode.Digital(__instance.RightBumper), __instance);
					commonTool.Secondary.AddBinding(InputNode.Digital(__instance.LeftBumper), __instance);
				}
			} else {
				globalActions.ToggleDash.AddBinding(__instance.Menu);
				globalActions.ToggleDash.AddBinding(__instance.Start);
			}
			*/

			/*
			if (group is ContextMenuInputs contextMenu) {


			}*/

			// TODO VR_LocomotionDirection, LocomotionTurn

			if (group is SmoothLocomotionInputs smoothLocomotion) {
				smoothLocomotion.Move.AddBinding(GenerateScreenDirection(__instance), __instance);
				smoothLocomotion.Jump.AddBinding(__instance.LeftThumbstickClick);
				smoothLocomotion.TurnDelta.AddBinding(GenerateTurn(__instance), __instance);
			}


			if (group is InteractionHandlerInputs commonTool) {

				if (commonTool.Side == Chirality.Left) {
					Msg("Added Left InteractionHandlerInputs");
					//commonTool.Interact.AddBinding(InputNode.Digital(Chirality.Right, __instance.A.Name), __instance);
					//commonTool.Interact.AddBinding(InputNode.Digital(Chirality.Left, __instance.X.Name), __instance);
					commonTool.Interact.AddBinding(__instance.X);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.Y), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.Y).TapToggle(), __instance);
					commonTool.Menu.AddBinding(InputNode.Digital(__instance.LeftBumper), __instance);
					commonTool.Secondary.AddBinding(__instance.Menu);
				}

				if (commonTool.Side == Chirality.Right) {
					Msg("Added Right InteractionHandlerInputs");
					//commonTool.Grab.AddBinding(InputNode.Digital(Chirality.Right, __instance.B.Name), __instance);
					//commonTool.Grab.AddBinding(
					//commonTool.Grab.AddBinding(InputNode.Digital(Chirality.Left, __instance.Y.Name), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.B), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.B).TapToggle(), __instance);
					commonTool.Interact.AddBinding(__instance.A);
					commonTool.Menu.AddBinding(InputNode.Digital(__instance.RightBumper), __instance);
				}

			}


				if (group is GlobalActions globalActions) {
				//globalActions.ToggleDash.AddBinding(__instance.Menu); // left
				//globalActions.ToggleDash.AddBinding(__instance.Start); //right

				globalActions.ToggleDash.AddBinding(InputNode.Digital(__instance.Start), __instance);

				/* TODO EM Would be great if I could figure out a "double click Y,B to open dash" ... but 
				 * the obvious isn't working ...
				MultiTapInput mtt = InputNode.Digital(__instance.B).MultiTapToggle();
				mtt.Sources.Add(InputNode.Digital(__instance.Y));
				globalActions.ToggleDash.AddBinding(mtt, __instance);
				*/
									
			}

			// Don't let the original method run. 
			return false;

		}


	}


	private void StripStandardGamepadBindings(InteractionHandlerInputs hand) {
		if (hand == null) return;
		PurgeBool((InputAction<bool>)hand.Interact);
		PurgeBool((InputAction<bool>)hand.Secondary);
		PurgeBool((InputAction<bool>)hand.Grab);
		PurgeBool((InputAction<bool>)hand.Menu);
		PurgeBool((InputAction<bool>)hand.UserspaceToggle);
		// PurgeFloat ((InputAction<float>)  hand.Strength);
		// PurgeFloat2((InputAction<float2>) hand.Axis);
	}

	public static InteractionHandlerInputs GetHandInputs(User user, Chirality chirality) {
		if (user?.Input == null) return null;

		// Each hand’s interaction inputs are stored under the InteractionHandlerInputModule
		var handler = user.GetInteractionHandler(chirality);
		return handler?.Inputs;
	}

	private void SetupBindings() {
		var world = Engine.Current.WorldManager.FocusedWorld;
		var user = world?.LocalUser;
		if (user == null) return;

		// 1) Get the left/right InteractionHandlerInputs ("commonTool" in your snippet)
		// Adjust accessors to your codebase; the idea is to get:
		//   leftCommon:  InteractionHandlerInputs  for left hand
		//   rightCommon: InteractionHandlerInputs  for right hand
		//var leftCommon = user.Input.LeftHand?.Interaction;   // aka your "commonTool" for LEFT
		//var rightCommon = user.Input.RightHand?.Interaction;  // aka your "commonTool" for RIGHT

		var leftCommon = GetHandInputs(world.LocalUser, Chirality.Left);
		var rightCommon = GetHandInputs(world.LocalUser, Chirality.Right);
		
		// 2) Fetch the built-in Gamepad device (so we reuse Resonite's deadzones & OS backends)
		var ii = Engine.Current.InputInterface;
		var gamepad = ii.GetDevice<StandardGamepad>();  // name may be IGamepadDevice / SDLGamepad / XInputGamepad in your build
		if (gamepad == null) return;

		// avoid double triggers
		if (leftCommon != null) StripStandardGamepadBindings(leftCommon);
		if (rightCommon != null) StripStandardGamepadBindings(rightCommon);


		//
		// We'll mirror that, but source the signals from the Gamepad device:
		//rightCommon.UserspaceToggle.AddBinding(gamepad.ButtonA);       // A -> right hand userspace toggle
		//rightCommon.Menu.AddBinding(gamepad.ButtonB);                  // B -> right hand menu
		//rightCommon.Grab.AddBinding(gamepad.RightShoulder);            // RB -> right hand grab
		//rightCommon.Secondary.AddBinding(gamepad.RightStickPress);     // RS -> right hand secondary
		//rightCommon.Interact.AddBinding(gamepad.RightTriggerClick);    // (edge) if exposed
		//rightCommon.Strength.AddBinding(gamepad.RightTrigger);         // RT analog -> Strength
		//rightCommon.Axis.AddBinding(gamepad.RightStick);               // right stick axes -> Axis
		rightCommon.UserspaceToggle.AddBinding(gamepad.Menu);
		rightCommon.Interact.AddBinding(gamepad.A);
		rightCommon.Grab.AddBinding(gamepad.B);
		rightCommon.Menu.AddBinding(gamepad.RightBumper);
		rightCommon.Secondary.AddBinding(gamepad.Start);


		// 4) Bind LEFT-HAND actions to X/Y/LB/LS and left stick/trigger
		//leftCommon.Interact.AddBinding(gamepad.ButtonX);               // X -> left hand interact (primary)
		//leftCommon.Secondary.AddBinding(gamepad.ButtonY);              // Y -> left hand secondary
		//leftCommon.Grab.AddBinding(gamepad.LeftShoulder);              // LB -> left hand grab
		//leftCommon.Secondary.AddBinding(gamepad.LeftStickPress);       // LS -> treat as secondary/alt
		//leftCommon.Strength.AddBinding(gamepad.LeftTrigger);           // LT analog -> Strength
		//leftCommon.Axis.AddBinding(gamepad.LeftStick);                 // left stick axes -> Axis
		//leftCommon.UserspaceToggle.AddBinding(gamepad.Menu);
		leftCommon.Interact.AddBinding(gamepad.X);
		leftCommon.Grab.AddBinding(gamepad.Y);
		leftCommon.Menu.AddBinding(gamepad.LeftBumper);
		//leftCommon.Secondary.AddBinding(gamepad.Start);

		// TIP: If your Gamepad API doesn’t expose "RightTriggerClick", you can synthesize it:
		//   rightCommon.Interact.AddBinding(new EdgeBool(gamepad.RightTrigger, threshold: 0.5f));
		//
		// And if your API uses different names, the mapping is straightforward:
		//   ButtonA/B/X/Y, Left/RightShoulder, Left/RightStickPress, Left/RightTrigger, Left/RightStick (float2)
	}

	static void PurgeBool(InputAction<bool> action) => PurgeImpl(action);
	static void PurgeFloat(InputAction<float> action) => PurgeImpl(action);
	static void PurgeFloat2(InputAction<float2> action) => PurgeImpl(action);

	static void PurgeImpl<T>(InputAction<T> action) where T : struct 
	{
		if (action == null) return;

		var tAction = typeof(InputAction<T>);
		var fBindings = tAction.GetField("_bindings", BindingFlags.Instance | BindingFlags.NonPublic);
		if (fBindings == null) return;

		var list = fBindings.GetValue(action) as IList; // List<InputBinding<T>> as non-generic IList
		if (list == null) return;

		// iterate backwards so RemoveAt is safe
		for (int i = list.Count - 1; i >= 0; i--) {
			var binding = list[i];
			var dev = GetMember(binding, "Device") ?? GetMember(binding, "_device");
			if (dev is StandardGamepad)
				list.RemoveAt(i);
		}

	}

	static object GetMember(object obj, string name) {
		if (obj == null) return null;
		var t = obj.GetType();
		var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (p != null) return p.GetValue(obj);
		var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (f != null) return f.GetValue(obj);
		return null;
	}


}
