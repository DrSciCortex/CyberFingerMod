/* 
// SPDX-License-Identifier: LGPL-3.0-only
// Copyright (C) 2025 DrSciCortex
Portions © 2025 XDelta (Resonite Mod Template ExampleMod)
*/

using System;
using System.Collections;          // <-- this gives you IList
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using HarmonyLib;
using Renderite.Shared;
using ResoniteModLoader;
using static Elements.Core.Pool;

namespace CyberFingerMod;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class CyberFingerMod : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.1.2"; //Changing the version here updates it in all locations needed
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


	// The toggle you'll see in the UI (default: true)
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> HideVirtualKeyboardKey =
		new ModConfigurationKey<bool>(
			"HideVirtualKeyboard",
			"Don't show the virtual keyboard (With cyberfinger, one should be using a real one!) .",
			() => true // default value
		);

	// Helper so patch code can read the toggle safely
	internal static bool HideVirtualKeyboard =>
		_config?.GetValue(HideVirtualKeyboardKey) ?? true;


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

	// Implement support for gamepad inputs as emulated controllers in RawDataTool
	[HarmonyPatch(typeof(RawDataTool), "UpdateValues")]
	public static class RawDataTool_UpdateValues_Patch {
		// --- private fields we need to read ---
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<float>>> PrimaryStrengthStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<float>>>("_primaryStrengthStream");
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<float2>>> SecondaryAxisStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<float2>>>("_secondaryAxisStream");
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<bool>>> PrimaryStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<bool>>>("_primaryStream");
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<bool>>> SecondaryStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<bool>>>("_secondaryStream");

		static readonly AccessTools.FieldRef<RawDataTool, Sync<float>> RawStrengthRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<float>>("_rawStrength");
		static readonly AccessTools.FieldRef<RawDataTool, Sync<float2>> RawAxisRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<float2>>("_rawAxis");
		static readonly AccessTools.FieldRef<RawDataTool, Sync<bool>> RawPrimaryRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<bool>>("_rawPrimary");
		static readonly AccessTools.FieldRef<RawDataTool, Sync<bool>> RawSecondaryRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<bool>>("_rawSecondary");

		// gamepad manager internals
		static readonly System.Reflection.FieldInfo GMField =
			AccessTools.Field(typeof(InputInterface), "_gamepadManager");
		static readonly System.Reflection.FieldInfo GamepadsField =
			AccessTools.Field(typeof(GamepadManager), "_gamepads");

		static bool Prefix(RawDataTool __instance) {
			var worker = (Worker)__instance;
			var world = worker.World;
			var ah = ((Tool)__instance).ActiveHandler;

			bool primaryBlocked = ah?.BlockPrimary ?? false;
			bool secondaryBlocked = ah?.BlockSecondary ?? false;

			// --- 1) Controller contribution (exactly like original, but via FieldRefs) ---
			var strengthStream = PrimaryStrengthStreamRef(__instance).Target;
			var axisStream = SecondaryAxisStreamRef(__instance).Target;
			var primaryStream = PrimaryStreamRef(__instance).Target;
			var secondaryStream = SecondaryStreamRef(__instance).Target;

			float ctrlStrength = strengthStream != null ? strengthStream.Value : RawStrengthRef(__instance).Value;
			float2 ctrlAxis = axisStream != null ? axisStream.Value : RawAxisRef(__instance).Value;
			bool ctrlPrimary = primaryStream != null ? primaryStream.Value : RawPrimaryRef(__instance).Value;
			bool ctrlSecondary = secondaryStream != null ? secondaryStream.Value : RawSecondaryRef(__instance).Value;

			// --- 2) Gamepad contribution (StandardGamepad) ---
			float gpStrength = 0f;
			float2 gpAxis = float2.Zero;
			bool gpPrimary = false;
			bool gpSecondary = false;

			var inputInterface = worker.InputInterface;
			var gm = (GamepadManager)GMField.GetValue(inputInterface);
			var dict = gm != null
				? (Dictionary<string, StandardGamepad>)GamepadsField.GetValue(gm)
				: null;
			var gamepad = dict?.Values.FirstOrDefault();

			if (gamepad != null && (__instance.Equipped?.Value ?? false)) {
				//primaryBlocked = false; 
				//secondaryBlocked = false;
				var side = ah?.Side ?? Chirality.Left;

				Analog2D axisProp;
				Digital primaryProp;
				Digital secondaryProp;

				if (side == Chirality.Left) {
					axisProp = gamepad.LeftThumbstick;
					primaryProp = gamepad.A;                   // left primary = A
					secondaryProp = gamepad.LeftThumbstickClick;
				} else {
					axisProp = gamepad.RightThumbstick;
					primaryProp = gamepad.X;                   // right primary = B
					secondaryProp = gamepad.RightThumbstickClick;
				}

				// sample device streams on-the-fly (GetStream is cheap + cached in engine)
				var gpAxisStream = axisProp.GetStream(world);   // ValueStream<float2>
				var gpPrimaryStream = primaryProp.GetStream(world);   // ValueStream<bool>
				var gpSecondaryStream = secondaryProp.GetStream(world);  // ValueStream<bool>

				if (gpAxisStream != null) gpAxis = gpAxisStream.Value;
				if (gpPrimaryStream != null) gpPrimary = gpPrimaryStream.Value;
				if (gpSecondaryStream != null) gpSecondary = gpSecondaryStream.Value;

				// primary strength from primary button as 0/1
				gpStrength = gpPrimary ? 1f : 0f;
			}

			// --- 3) Merge controller + gamepad in *value space* ---

			// strength: sum then clamp to [0,1]
			//float mergedStrength = primaryBlocked ? 0f : Clamp01(ctrlStrength + gpStrength);
			float mergedStrength = gpStrength;

			// axis: sum then clamp per component to [-1,1] (sticks are typically -1..1)
			float2 mergedAxis = secondaryBlocked
				? float2.Zero
				: ClampAxis(ctrlAxis + gpAxis);

			//bool mergedPrimary = !primaryBlocked && (ctrlPrimary || gpPrimary);
			bool mergedSecondary = !secondaryBlocked && (ctrlSecondary || gpSecondary);
			bool mergedPrimary = gpPrimary;

			// --- 4) Write results into public RawOutputs ---
			__instance.PrimaryStrength.Value = mergedStrength;
			__instance.SecondaryAxis.Value = mergedAxis;
			__instance.Primary.Value = mergedPrimary;
			__instance.Secondary.Value = mergedSecondary;

			// skip original UpdateValues (we fully replaced it)
			return false;
		}

		static float Clamp01(float x) {
			if (x < 0f) return 0f;
			if (x > 1f) return 1f;
			return x;
		}

		static float2 ClampAxis(float2 v) {
			float lenSq = v.x * v.x + v.y * v.y;
			if (lenSq > 1f) {
				float inv = 1f / (float)Math.Sqrt(lenSq);
				return new float2(v.x * inv, v.y * inv);
			}
			return v;
		}

		static float ClampMinus1To1(float x) {
			if (x < -1f) return -1f;
			if (x > 1f) return 1f;
			return x;
		}
	}


	[HarmonyPatch(typeof(VirtualKeyboard), "IsShown", MethodType.Setter)]
	static class VK_IsShown_Set_Patch {
		// If you want a toggle, gate on your setting.
		static bool Prefix(ref bool value) {
			if (!HideVirtualKeyboard) return true; // allow normal behavior
			value = false; // force-hide
			return true;   // still run original setter (it will set ActiveSelf=false & null TargetText)
		}
	}


	//Implement the HarmonyPatch
	[HarmonyPatch(typeof(StandardGamepad), "Bind")]
	public static class StandardGamepad_Bind_Patch {

		static private ScreenLocomotionDirection GenerateScreenDirection(StandardGamepad __instance) {
			ScreenLocomotionDirection screenLocomotionDirection = new ScreenLocomotionDirection();

			var rs = InputNode.Analog2D(__instance.RightThumbstick);
			var ls = InputNode.Analog2D(__instance.LeftThumbstick);

			// 3) Sum: right + (-left)
			var ySum = new SumInputs<float>();
			ySum.Inputs.Add(rs.Y());
			ySum.Inputs.Add(ls.Y());

			screenLocomotionDirection.Axis = InputNode.XY(ls.X(), ySum);

			screenLocomotionDirection.Up = InputNode.Digital(__instance.RightThumbstickClick);
			screenLocomotionDirection.Down = InputNode.Digital(__instance.LeftThumbstickClick);
			return screenLocomotionDirection;
		}

		static private VR_SingleLocomotionTurn GenerateTurn(StandardGamepad __instance) {
			return new VR_SingleLocomotionTurn(InputNode.Analog2D(__instance.RightThumbstick).X());
		}

		static private VR_SingleLocomotionTurn GenerateTurnLeft(StandardGamepad __instance) {
			return new VR_SingleLocomotionTurn(InputNode.Analog2D(__instance.LeftThumbstick).X());
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

			
			if (group is SmoothThreeAxisLocomotionInputs threeAxisLocomotion) {
				threeAxisLocomotion.Move.AddBinding(GenerateScreenDirection(__instance), __instance);
				threeAxisLocomotion.Jump.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
				threeAxisLocomotion.Align.AddBinding(InputNode.Digital(__instance.B), __instance);
				threeAxisLocomotion.Align.AddBinding(InputNode.Digital(__instance.Y), __instance);

				var rs = InputNode.Analog2D(__instance.RightThumbstick);
				var pitch = rs.Y().Negate();  // invert so up = pitch up
				var yaw = rs.X();

				var right = InputNode.Digital(__instance.X).ToAnalog();
				var left = InputNode.Digital(__instance.A).ToAnalog();

				// 2) Negate the "left" (either via .Negate() or multiply by -1)
				var leftNeg = left.Negate();

				// 3) Sum: right + (-left)
				var rollSum = new SumInputs<float>();
				rollSum.Inputs.Add(right);
				rollSum.Inputs.Add(leftNeg);

				var turn = InputNode.XYZ(pitch, yaw, rollSum);
				threeAxisLocomotion.TurnDelta.AddBinding(turn, __instance);
			}

			if (group is AnchorLocomotionInputs anchorInputs) {

				anchorInputs.PrimaryAxis.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick), __instance);
				anchorInputs.SecondaryAxis.AddBinding(InputNode.Analog2D(__instance.RightThumbstick), __instance);
				anchorInputs.PrimaryAction.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance);
				anchorInputs.SecondaryAction.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
			}

			if (group is AnchorReleaseInputs anchorRelease) {
				anchorRelease.Release.AddBinding(InputNode.Digital(__instance.A), __instance);
				anchorRelease.Release.AddBinding(InputNode.Digital(__instance.X), __instance);
				anchorRelease.ReleaseStrength.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick).Magnitude(), __instance);
				anchorRelease.ReleaseStrength.AddBinding(InputNode.Analog2D(__instance.RightThumbstick).Magnitude(), __instance);
			}

			if (group is TeleportInputs teleport) {
				if (teleport.Side == Chirality.Left) {
					teleport.Teleport.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance);
					teleport.Backstep.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick).Y().Negate()
						.ToDigital(0.8f), __instance);
					teleport.TurnDelta.AddBinding(GenerateTurnLeft(__instance), __instance);
				}

				if (teleport.Side == Chirality.Right) {
					teleport.Teleport.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
					teleport.Backstep.AddBinding(InputNode.Analog2D(__instance.RightThumbstick).Y().Negate()
						.ToDigital(0.8f), __instance);
					teleport.TurnDelta.AddBinding(GenerateTurn(__instance), __instance);
				}

			}

			if (group is DevToolInputs devTool) {

				if (devTool.Side == Chirality.Left) {
					Msg("Added Left DevToolInputs");
					devTool.Focus.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance, null, 20000);
					devTool.Inspector.AddBinding(InputNode.Digital(__instance.X), __instance, null, 20000);
					devTool.Create.AddBinding(InputNode.Digital(__instance.Y), __instance, null, 20000);
					//devTool.Create.AddBinding(InputNode.Digital(__instance.Menu), __instance, null, -100);
					//commonTool.Secondary.AddBinding(InputNode.Digital(__instance.Menu), __instance);
				}

				if (devTool.Side == Chirality.Right) {
					Msg("Added Right DevToolInputs");
					devTool.Focus.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance, null, 20000);
					devTool.Inspector.AddBinding(InputNode.Digital(__instance.A), __instance, null, 20000);
					devTool.Create.AddBinding(InputNode.Digital(__instance.B), __instance, null, 20000);
					//devTool.Create.AddBinding(InputNode.Digital(__instance.Menu), __instance, null, -100);

				}

			}

			if (group is LaserHoldInputs laserHold) {
				if (laserHold.Side == Chirality.Left) {
					laserHold.Align.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance);
					laserHold.Slide.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick).Y(), __instance);
					laserHold.Rotate.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick).X(), __instance);
				}

				if (laserHold.Side == Chirality.Right) {
					laserHold.Align.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
					laserHold.Slide.AddBinding(InputNode.Analog2D(__instance.RightThumbstick).Y(), __instance);
					laserHold.Rotate.AddBinding(InputNode.Analog2D(__instance.RightThumbstick).X(), __instance);
				}
			}


			if (group is HeadInputs headInputs) {
				headInputs.Crouch.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick).ToAnalog(), __instance);
				headInputs.Crouch.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick).MultiTapToggle().ToAnalog(), __instance);
			}

			if (group is SmoothLocomotionInputs smoothLocomotion) {
				smoothLocomotion.Move.AddBinding(GenerateScreenDirection(__instance), __instance);
				smoothLocomotion.Jump.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
				smoothLocomotion.Jump.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance);
				smoothLocomotion.TurnDelta.AddBinding(GenerateTurn(__instance), __instance);
			}

			if (group is InteractionHandlerInputs commonTool) {

				if (commonTool.Side == Chirality.Left) {
					Msg("Added Left InteractionHandlerInputs");
					//commonTool.Interact.AddBinding(InputNode.Digital(Chirality.Right, __instance.A.Name), __instance);
					//commonTool.Interact.AddBinding(InputNode.Digital(Chirality.Left, __instance.X.Name), __instance);
					commonTool.Strength.AddBinding(InputNode.Digital(__instance.X).ToAnalog(), __instance);
					commonTool.Interact.AddBinding(InputNode.Digital(__instance.X), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.Y), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.Y).TapToggle(), __instance);
					commonTool.Menu.AddBinding(InputNode.Digital(__instance.LeftBumper), __instance);
					commonTool.Secondary.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance);
				}

				if (commonTool.Side == Chirality.Right) {
					Msg("Added Right InteractionHandlerInputs");
					//commonTool.Grab.AddBinding(InputNode.Digital(Chirality.Right, __instance.B.Name), __instance);
					//commonTool.Grab.AddBinding(
					//commonTool.Grab.AddBinding(InputNode.Digital(Chirality.Left, __instance.Y.Name), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.B), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.B).TapToggle(), __instance);
					commonTool.Interact.AddBinding(InputNode.Digital(__instance.A), __instance);
					commonTool.Strength.AddBinding(InputNode.Digital(__instance.A).ToAnalog(), __instance);
					commonTool.Menu.AddBinding(InputNode.Digital(__instance.RightBumper), __instance);
					commonTool.Secondary.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
				}

			}


			if (group is GlobalActions globalActions) {
				//globalActions.ToggleDash.AddBinding(__instance.Menu); // left
				//globalActions.ToggleDash.AddBinding(__instance.Start); //right

				globalActions.ToggleDash.AddBinding(InputNode.Digital(__instance.Start), __instance);
				globalActions.ToggleDash.AddBinding(InputNode.Digital(__instance.Menu), __instance);

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

}
