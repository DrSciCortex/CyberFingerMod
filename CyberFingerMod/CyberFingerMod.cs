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
	internal const string VERSION_CONSTANT = "1.1.1"; //Changing the version here updates it in all locations needed
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

	[HarmonyPatch(typeof(VirtualKeyboard), "IsShown", MethodType.Setter)]
	static class VK_IsShown_Set_Patch {
		// If you want a toggle, gate on your setting.
		static bool Prefix(ref bool value) {
			if (!HideVirtualKeyboard) return true; // allow normal behavior
			value = false; // force-hide
			return true;   // still run original setter (it will set ActiveSelf=false & null TargetText)
		}
	}

	// x > t  -> (x - t).ToDigital(activation:0)   (true if positive)
	static IInputNode<bool> GreaterThan(IInputNode<float> x, float t) {
		var s = new SumInputs<float>();
		s.Inputs.Add(x);
		s.Inputs.Add(InputNode.Constant(-t)); // x - t
		return s.ToDigital(0f, 0f);
	}

	// x < t  -> (t - x).ToDigital(activation:0)
	static IInputNode<bool> LessThan(IInputNode<float> x, float t) {
		var s = new SumInputs<float>();
		s.Inputs.Add(InputNode.Constant(t));  // t - x
		s.Inputs.Add(x.Negate());
		return s.ToDigital(0f, 0f);
	}

	// Clamp a float node to [0,1] using the conditional
	static IInputNode<float> Clamp01F(IInputNode<float> x) {
		var gt1 = GreaterThan(x, 1f);   // x > 1
		var lt0 = LessThan(x, 0f);      // x < 0

		// First clamp top: hi = gt1 ? 1 : x
		var hi = InputNode.Conditional(InputNode.Constant(1f), x, gt1);

		// Then clamp bottom: res = lt0 ? 0 : hi
		return InputNode.Conditional(InputNode.Constant(0f), hi, lt0);
	}

	// Per-component clamp for float2 using X()/Y() and XY(...)
	static IInputNode<float2> Clamp01F2(IInputNode<float2> v) {
		var cx = Clamp01F(v.X());
		var cy = Clamp01F(v.Y());
		return InputNode.XY(cx, cy);
	}

	// Bool OR: using Any(...)
	static IInputNode<bool> OrBool(IInputNode<bool> a, IInputNode<bool> b) {
		if (a == null) return b;
		if (b == null) return a;
		return InputNode.Any(a, b);
	}

	// Clamped sums (combine first, then clamp)
	static IInputNode<float> SumClamped01(IInputNode<float> a, IInputNode<float> b) {
		if (a == null) return b;
		if (b == null) return a;
		var s = new SumInputs<float>(); s.Inputs.Add(a); s.Inputs.Add(b);
		return Clamp01F(s);
	}

	static IInputNode<float2> SumClamped01(IInputNode<float2> a, IInputNode<float2> b) {
		if (a == null) return b;
		if (b == null) return a;
		var s = new SumInputs<float2>(); s.Inputs.Add(a); s.Inputs.Add(b);
		return Clamp01F2(s);
	}

	//Implement the HarmonyPatch
	//Fix RawDataTool so it will also get StandardGamepad input (merge with any vr controller)
	[HarmonyPatch(typeof(RawDataTool), "UpdateSourceController")]
	public static class RawDataTool_UpdateSourceController_Patch {

		static bool Prefix(RawDataTool __instance) {
			IStandardController controller = null;
			var _ah = ((Tool)__instance).ActiveHandler;
			var _world = ((Worker)__instance).World;
			var _inputInterface = ((Worker)__instance).InputInterface;
			var gmField = AccessTools.Field(typeof(InputInterface), "_gamepadManager");
			var gm = (GamepadManager)gmField.GetValue(_inputInterface);
			var dictField = AccessTools.Field(typeof(GamepadManager), "_gamepads");
			var dict = (Dictionary<string, StandardGamepad>)dictField.GetValue(gm);
			var gamepad = (StandardGamepad)dict?.Values.FirstOrDefault();

			if (_ah != null) {
				controller = ((Worker)__instance).InputInterface.GetControllerNode(_ah.Side);
			}

			// __instance._sourceController 
			var scField = AccessTools.Field(typeof(RawDataTool), "_sourceController");
			var sc = (IStandardController)scField.GetValue(__instance);

			var PrimaryStrengthRef = AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<float>>>("_primaryStrengthStream");
			var SecondaryAxisRef = AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<float2>>>("_secondaryAxisStream");
			var PrimaryRef = AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<bool>>>("_primaryStream");
			var SecondaryRef = AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<bool>>>("_secondaryStream");

			if (sc != controller) {
				__instance.ControllerType.Value = controller?.GetType();
				Sync<Chirality> controllerSide = __instance.ControllerSide;
				Sync<Chirality> sync = _ah?.Side;
				controllerSide.Value = ((sync != null) ? ((Chirality)sync) : ((Chirality)(-1)));
				var side = _ah?.Side ?? Chirality.Left;

				Analog2D Axis = null;
				Digital Primary = null;
				Analog PrimaryStrength = null; // use trigger if you want analog strength
				Digital Secondary = null;

				// give priority to gamepad
				// TODO: ideally the streams of gamepad and VR could be merged
				if (gamepad != null) {
					if (side == Chirality.Left) {
						Axis = gamepad.LeftThumbstick;
						Primary = gamepad.A;                 // left primary = A (digital)
						// TODO -> this shuold be Analog of A
						PrimaryStrength = gamepad.LeftTrigger;                         // primaryStrength = A -> analog
						Secondary = gamepad.LeftThumbstickClick;
					} else // Chirality.Right
					  {
						Axis = gamepad.RightThumbstick;
						Primary = gamepad.B;                 // right primary = B (digital)
						// TODO -> this shuold be Analog of B
						PrimaryStrength = gamepad.RightTrigger;                         // primaryStrength = B -> analog
						Secondary = gamepad.RightThumbstickClick;
					}
				} else {

					// VR controller nodes (may be null)
					PrimaryStrength = controller?.Strength;
					Axis = controller?.Axis;
					Primary = controller?.ActionPrimary;
					Secondary = controller?.ActionSecondary;

					PrimaryStrengthRef(__instance).Target = PrimaryStrength?.GetStream(_world);
					SecondaryAxisRef(__instance).Target = Axis?.GetStream(_world);
					PrimaryRef(__instance).Target = Primary?.GetStream(_world);
					SecondaryRef(__instance).Target = Secondary?.GetStream(_world);
				}

				/*
				// Merge
				var mergedStrength = SumClamped01(vrStrength, gpPrimaryStrength);
				var mergedAxis = SumClamped01(vrAxis, gpAxis);
				var mergedPrimary = OrBool(vrPrimary, gpPrimary);
				var mergedSecondary = OrBool(vrSecondary, gpSecondary);

				// Assign streams
				PrimaryStrengthRef(__instance).Target = mergedStrength?.GetStream(_world);
				SecondaryAxisRef(__instance).Target = mergedAxis?.GetStream(_world);
				PrimaryRef(__instance).Target = mergedPrimary?.GetStream(_world);
				SecondaryRef(__instance).Target = mergedSecondary?.GetStream(_world);
				*/

				scField.SetValue(__instance, controller);
			}


			return false;
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
