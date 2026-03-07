/* 
// SPDX-License-Identifier: LGPL-3.0-only
// Copyright (C) 2025 DrSciCortex
Portions © 2025 XDelta (Resonite Mod Template ExampleMod)
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using HarmonyLib;
using Renderite.Shared;
using ResoniteModLoader;
using static Elements.Core.Pool;

namespace CyberFingerMod;

public class CyberFingerMod : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.8.28";
	public override string Name => "CyberFingerMod";
	public override string Author => "DrSciCortex";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DrSciCortex/CyberFingerMod/";

	private static ModConfiguration? _config;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> ForceOverrideKey =
		new ModConfigurationKey<bool>(
			"ForceOverride",
			"Force userspace laser override (skip engine's userspace branch and use mod logic).",
			() => true
		);
	internal static bool ForceOverride => _config?.GetValue(ForceOverrideKey) ?? true;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> HideVirtualKeyboardKey =
		new ModConfigurationKey<bool>(
			"HideVirtualKeyboard",
			"Don't show the virtual keyboard (With cyberfinger, one should be using a real one!).",
			() => true
		);
	internal static bool HideVirtualKeyboard => _config?.GetValue(HideVirtualKeyboardKey) ?? true;

	// ── Improved laser filtering ───────────────────────────────────────────

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> ImprovedFilteringKey =
		new ModConfigurationKey<bool>(
			"ImprovedFiltering",
			"Enable improved laser filtering (sternum spatial blend + UKF filter).",
			() => true
		);
	internal static bool ImprovedFiltering => _config?.GetValue(ImprovedFilteringKey) ?? true;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> FilteringEnabledKey =
		new ModConfigurationKey<bool>(
			"FilteringEnabled",
			"Enable laser pointer filtering. Set true for hand tracking, false for physical controllers.",
			() => true
		);
	internal static bool FilteringEnabled => _config?.GetValue(FilteringEnabledKey) ?? true;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float> ArmBlendWeightKey =
		new ModConfigurationKey<float>(
			"ArmBlendWeight",
			"Sternum-to-hand spatial blend weight (0=fingertip only, 1=sternum-dir only). Default: 0.45",
			() => 0.45f
		);
	internal static float ArmBlendWeight => _config?.GetValue(ArmBlendWeightKey) ?? 0.45f;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float> SternumOffsetKey =
		new ModConfigurationKey<float>(
			"SternumOffset",
			"Vertical offset from head position to sternum anchor, in metres (negative = below head). Default: -0.20",
			() => -0.20f
		);
	internal static float SternumOffset => _config?.GetValue(SternumOffsetKey) ?? -0.20f;

	// ── UKF parameters ────────────────────────────────────────────────────

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float> KalmanProcessNoiseKey =
		new ModConfigurationKey<float>(
			"KalmanProcessNoise",
			"[UKF] Process noise Q — how fast the direction can legitimately change. Lower = smoother, higher = more responsive. Default: 1000",
			() => 1000f
		);
	internal static float KalmanProcessNoise => _config?.GetValue(KalmanProcessNoiseKey) ?? 1000f;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float> KalmanMeasurementNoiseKey =
		new ModConfigurationKey<float>(
			"KalmanMeasurementNoise",
			"[UKF] Measurement noise R — how much to distrust raw hand-tracker data. Higher = smoother, lower = more responsive. Default: 200",
			() => 200f
		);
	internal static float KalmanMeasurementNoise => _config?.GetValue(KalmanMeasurementNoiseKey) ?? 200f;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float> KalmanVelocityDampingKey =
		new ModConfigurationKey<float>(
			"KalmanVelocityDamping",
			"[UKF] Velocity decay per second (0=no damping, 1=instant stop). Higher reduces overshoot. Default: 0.85",
			() => 0.85f
		);
	internal static float KalmanVelocityDamping => _config?.GetValue(KalmanVelocityDampingKey) ?? 0.85f;

	// ── Dead-band parameters ─────────────────────────────────────────────

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float> DeadBandAngleKey =
		new ModConfigurationKey<float>(
			"DeadBandAngle",
			"Dead-band radius in degrees. Kalman output is frozen while it stays within this cone of the locked direction. 0 = disabled. Default: 1.5",
			() => 1.5f
		);
	internal static float DeadBandAngle => _config?.GetValue(DeadBandAngleKey) ?? 1.5f;

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float> DeadBandReleaseSpeedKey =
		new ModConfigurationKey<float>(
			"DeadBandReleaseSpeed",
			"Angular speed (deg/s) at which the dead-band fully releases. Above this speed the output tracks normally. Default: 8.0",
			() => 8.0f
		);
	internal static float DeadBandReleaseSpeed => _config?.GetValue(DeadBandReleaseSpeedKey) ?? 8.0f;

	// ── End filtering config ───────────────────────────────────────────────

	public override void OnEngineInit() {
		_config = GetConfiguration();
		_config?.Save(true);

		Harmony harmony = new Harmony("com.scicortex.CyberFingerMod");
		harmony.PatchAll();

		Msg("Initalized CyberFingerMod");
	}

	// ── ModSettings custom UI integration ─────────────────────────────────
	// ModSettings calls ModSettings_BuildModUi instead of its default field
	// generator, letting us set item height to 2x the user's global setting.
	public void ModSettings_BuildModUi(UIBuilder ui) {
		float baseHeight = ModSettings_GetHeight();
		ui.Style.PreferredHeight = baseHeight * 2f;
		ui.Style.TextAutoSizeMax = baseHeight * 2f;
		ModSettings_BuildDefaultFields(this, ui);
	}

	// Stubs — ModSettings patches these at runtime via PublicApiMethods.
	public static float ModSettings_GetHeight()
		=> throw new NotImplementedException("Stub — patched by ModSettings at runtime.");
	public static colorX? ModSettings_GetHighlightColor()
		=> throw new NotImplementedException("Stub — patched by ModSettings at runtime.");
	public static void ModSettings_BuildDefaultFields(ResoniteModBase mod, UIBuilder ui)
		=> throw new NotImplementedException("Stub — patched by ModSettings at runtime.");
	public static Slot ModSettings_BuildDefaultField(ResoniteModBase mod, UIBuilder ui, ModConfigurationKey key)
		=> throw new NotImplementedException("Stub — patched by ModSettings at runtime.");
	// ── End ModSettings integration ────────────────────────────────────────


	// ══════════════════════════════════════════════════════════════════════
	//  CONSTANT-VELOCITY KALMAN FILTER  (direction only, spherical coords)
	//
	//  State vector: [θ, φ, θ̇, φ̇]  (azimuth, elevation, their velocities)
	//  Models the direction as moving at roughly constant angular velocity,
	//  with small random accelerations as process noise.
	//
	//  This handles correlated lateral jitter on straight-line sweeps much
	//  better than One Euro because it explicitly predicts where the direction
	//  *should* be based on current velocity, and only corrects toward the
	//  measurement weighted by how much it trusts the sensor (R) vs the model (Q).
	//
	//  Two tuning parameters:
	//    ProcessNoise Q  — expected angular acceleration variance (rad²/s³)
	//                      small = smoother but slower to react to direction changes
	//    MeasurementNoise R — hand-tracker noise variance (rad²)
	//                      large = smoother but more lag
	// ══════════════════════════════════════════════════════════════════════
	// ══════════════════════════════════════════════════════════════════════
	//  UNSCENTED KALMAN FILTER — spherical coordinate direction tracking
	//
	//  State vector x = [θ, φ, θ̇, φ̇]  (n=4)
	//  Process model: constant angular velocity with optional damping
	//    θ_{k+1} = θ_k + θ̇_k·dt
	//    φ_{k+1} = φ_k + φ̇_k·dt
	//    θ̇_{k+1} = θ̇_k · decay
	//    φ̇_{k+1} = φ̇_k · decay
	//  Measurement: z = [θ_meas, φ_meas]  (m=2)
	//
	//  UKF parameters:
	//    alpha  — spread of sigma points around mean (1e-3 to 1.0)
	//    beta   — prior knowledge of distribution (2.0 optimal for Gaussian)
	//    kappa  — secondary scaling (usually 0 or 3-n)
	//
	//  2n+1 = 9 sigma points per step. No linearization — the nonlinear
	//  spherical dynamics are propagated exactly through each sigma point,
	//  giving accurate mean/covariance without the overshoot that comes
	//  from linearization error in the standard KF.
	// ══════════════════════════════════════════════════════════════════════
	internal sealed class UKFDirectionFilter {
		private const int N = 4;  // state dimension
		private const int M = 2;  // measurement dimension
		private const int NS = 2*N+1; // 9 sigma points

		// State
		private float _theta, _phi, _dtheta, _dphi;
		// Covariance (4x4 row-major)
		private readonly float[] _P   = new float[N*N];
		private bool _initialised;

		// ── Preallocated working buffers (zero GC per frame) ──────────────
		// sigma and sigmaP: NS×N stored as flat [i*N+j]
		private readonly float[] _sigma  = new float[NS*N];
		private readonly float[] _sigmaP = new float[NS*N];
		private readonly float[] _x0     = new float[N];
		private readonly float[] _xp     = new float[N];
		private readonly float[] _Pp     = new float[N*N];
		private readonly float[] _Pxz    = new float[N*M];
		private readonly float[] _K      = new float[N*M];
		private readonly float[] _L      = new float[N*N];  // Cholesky output
		private readonly float[] _dx     = new float[N];

		// UKF scaling parameters
		private const float Alpha = 0.1f;   // sigma point spread
		private const float Beta  = 2.0f;   // Gaussian prior
		private const float Kappa = 0.0f;   // secondary scaling
		private static readonly float Lambda = Alpha*Alpha*(N+Kappa) - N;
		private static readonly float Wm0 = Lambda / (N + Lambda);
		private static readonly float Wc0 = Wm0 + (1f - Alpha*Alpha + Beta);
		private static readonly float Wi  = 1f / (2f*(N + Lambda));

		public void Reset() => _initialised = false;

		public float3 Filter(in float3 direction, float dt, float Q, float R, float velocityDamping = 0f) {
			float3 d = direction.Normalized;
			float mTheta = MathX.Atan2(d.x, d.z);
			float mPhi   = MathX.Asin(MathX.Clamp(d.y, -1f, 1f));

			if (!_initialised) {
				_theta = mTheta; _phi = mPhi; _dtheta = 0f; _dphi = 0f;
				InitP(1f);
				_initialised = true;
				return direction;
			}

			dt = MathX.Clamp(dt, 0.001f, 0.1f);

			// ── Build noise scalars ────────────────────────────────────
			float dt2 = dt*dt;
			float qp = Q * dt2 * dt / 3f;
			float qv = Q * dt;
			float qc = Q * dt2 / 2f;

			// ── Cholesky of P into preallocated _L ────────────────────
			float spread = MathX.Sqrt(N + Lambda);
			if (!Cholesky(_P, _L)) { InitP(1f); return direction; }

			// ── Generate sigma points into _sigma (flat NS×N) ─────────
			_x0[0] = _theta; _x0[1] = _phi; _x0[2] = _dtheta; _x0[3] = _dphi;
			for (int j = 0; j < N; j++) _sigma[0*N+j] = _x0[j];
			for (int i = 0; i < N; i++) {
				for (int j = 0; j < N; j++) {
					float col = _L[j*N+i] * spread;
					_sigma[(1+i)*N+j]   = _x0[j] + col;
					_sigma[(1+i+N)*N+j] = _x0[j] - col;
				}
				_sigma[(1+i)*N+0]   = WrapAngle(_sigma[(1+i)*N+0]);
				_sigma[(1+i+N)*N+0] = WrapAngle(_sigma[(1+i+N)*N+0]);
			}

			// ── Propagate through process model into _sigmaP ──────────
			float decay = velocityDamping > 0f
				? MathX.Pow(1f - MathX.Clamp01(velocityDamping), dt)
				: 1f;
			for (int i = 0; i < NS; i++) {
				int b = i*N;
				_sigmaP[b+0] = WrapAngle(_sigma[b+0] + _sigma[b+2]*dt);
				_sigmaP[b+1] = _sigma[b+1] + _sigma[b+3]*dt;
				_sigmaP[b+2] = _sigma[b+2] * decay;
				_sigmaP[b+3] = _sigma[b+3] * decay;
			}

			// ── Predicted mean into _xp ────────────────────────────────
			for (int j = 0; j < N; j++) _xp[j] = (float)Wm0 * _sigmaP[0*N+j];
			for (int i = 1; i < NS; i++)
				for (int j = 0; j < N; j++) _xp[j] += (float)Wi * _sigmaP[i*N+j];
			_xp[0] = WrapAngle(_xp[0]);

			// ── Predicted covariance into _Pp ─────────────────────────
			for (int k = 0; k < N*N; k++) _Pp[k] = 0f;
			for (int i = 0; i < NS; i++) {
				float w = (i == 0) ? (float)Wc0 : (float)Wi;
				for (int j = 0; j < N; j++) _dx[j] = _sigmaP[i*N+j] - _xp[j];
				_dx[0] = WrapAngle(_dx[0]);
				for (int r = 0; r < N; r++)
					for (int c = 0; c < N; c++)
						_Pp[r*N+c] += w * _dx[r] * _dx[c];
			}
			_Pp[0*N+0] += qp; _Pp[0*N+2] += qc; _Pp[2*N+0] += qc;
			_Pp[1*N+1] += qp; _Pp[1*N+3] += qc; _Pp[3*N+1] += qc;
			_Pp[2*N+2] += qv;
			_Pp[3*N+3] += qv;

			// ── Predicted measurement mean ─────────────────────────────
			float zp0 = (float)Wm0 * _sigmaP[0*N+0];
			float zp1 = (float)Wm0 * _sigmaP[0*N+1];
			for (int i = 1; i < NS; i++) {
				zp0 += (float)Wi * _sigmaP[i*N+0];
				zp1 += (float)Wi * _sigmaP[i*N+1];
			}
			zp0 = WrapAngle(zp0);

			// ── Innovation covariance S + cross covariance _Pxz ──────
			float s00=R, s11=R, s01=0f;
			for (int k = 0; k < N*M; k++) _Pxz[k] = 0f;
			for (int i = 0; i < NS; i++) {
				float w = (i == 0) ? (float)Wc0 : (float)Wi;
				float dz0 = WrapAngle(_sigmaP[i*N+0] - zp0);
				float dz1 = _sigmaP[i*N+1] - zp1;
				s00 += w * dz0 * dz0;
				s11 += w * dz1 * dz1;
				s01 += w * dz0 * dz1;
				for (int j = 0; j < N; j++) _dx[j] = _sigmaP[i*N+j] - _xp[j];
				_dx[0] = WrapAngle(_dx[0]);
				for (int r = 0; r < N; r++) {
					_Pxz[r*M+0] += w * _dx[r] * dz0;
					_Pxz[r*M+1] += w * _dx[r] * dz1;
				}
			}

			// ── Kalman gain _K = _Pxz * S⁻¹ ──────────────────────────
			float detS = s00*s11 - s01*s01;
			if (MathX.Abs(detS) < 1e-9f) { Array.Copy(_Pp, _P, N*N); return direction; }
			float invDet = 1f / detS;
			for (int r = 0; r < N; r++) {
				_K[r*M+0] = (_Pxz[r*M+0]*s11 - _Pxz[r*M+1]*s01) * invDet;
				_K[r*M+1] = (_Pxz[r*M+1]*s00 - _Pxz[r*M+0]*s01) * invDet;
			}

			// ── State update ───────────────────────────────────────────
			float inn0 = WrapAngle(mTheta - zp0);
			float inn1 = mPhi - zp1;
			_theta  = WrapAngle(_xp[0] + _K[0*M+0]*inn0 + _K[0*M+1]*inn1);
			_phi    = MathX.Clamp(_xp[1] + _K[1*M+0]*inn0 + _K[1*M+1]*inn1, -1.5f, 1.5f);
			_dtheta = _xp[2] + _K[2*M+0]*inn0 + _K[2*M+1]*inn1;
			_dphi   = _xp[3] + _K[3*M+0]*inn0 + _K[3*M+1]*inn1;

			// ── Covariance update: P = Pp - K*S*Kᵀ ────────────────────
			for (int r = 0; r < N; r++)
				for (int c = 0; c < N; c++) {
					float ks0 = _K[r*M+0]*s00 + _K[r*M+1]*s01;
					float ks1 = _K[r*M+0]*s01 + _K[r*M+1]*s11;
					_P[r*N+c] = _Pp[r*N+c] - (ks0*_K[c*M+0] + ks1*_K[c*M+1]);
				}

			float cosP = MathX.Cos(_phi);
			return new float3(cosP * MathX.Sin(_theta), MathX.Sin(_phi), cosP * MathX.Cos(_theta));
		}

		// Cholesky decomposition: writes lower triangular L into buf such that L*Lᵀ = A.
		// Returns false if A is not positive definite.
		private static bool Cholesky(float[] A, float[] buf) {
			for (int i = 0; i < N*N; i++) buf[i] = 0f;
			for (int i = 0; i < N; i++) {
				for (int j = 0; j <= i; j++) {
					float s = A[i*N+j];
					for (int k = 0; k < j; k++) s -= buf[i*N+k] * buf[j*N+k];
					if (i == j) {
						if (s <= 0f) return false;
						buf[i*N+j] = MathX.Sqrt(s);
					} else {
						buf[i*N+j] = s / buf[j*N+j];
					}
				}
			}
			return true;
		}

		private void InitP(float scale) {
			for (int i = 0; i < N*N; i++) _P[i] = 0f;
			for (int i = 0; i < N; i++) _P[i*N+i] = scale;
		}

		private static float WrapAngle(float a) {
			while (a >  MathX.PI) a -= 2f * MathX.PI;
			while (a < -MathX.PI) a += 2f * MathX.PI;
			return a;
		}
	}

	// ══════════════════════════════════════════════════════════════════════
	//  PER-SIDE FILTER STATE
	//  Userspace and world-space each get their OWN filter state dictionaries.
	//  Sharing state between the two patches causes the filter history to be
	//  corrupted (wrong dt, wrong _xFilt baseline) because both patches run
	//  in the same frame on different coordinate spaces.
	// ══════════════════════════════════════════════════════════════════════
	internal sealed class SideFilterState {
		public readonly UKFDirectionFilter UKFFilter = new UKFDirectionFilter();
		public double LastTime      = -1.0;
		public float3 PrevForward   = float3.Forward;
		// Dead-band: the direction the output is currently locked to.
		// Null = not yet initialised (pass through on first frame).
		public float3? LockedForward = null;
	}

	// ── Hand-tracking detection ──────────────────────────────────────────────
	// HandPoser.PoseSource.Target is non-null only when hand tracking actively
	// drives the skeleton. With physical controllers the HandPoser exists on
	// the avatar but PoseSource.Target is null. We walk it directly every frame
	// — no cache needed; called from patches that fire every frame for the local user.
	internal static bool AreHandsVisible(InteractionHandler handler) {
		try {
			if (!(handler.InputInterface?.VR_Active ?? false)) return false;
			Chirality side = handler.Side.Value;
			// Use the most recently registered hand (highest DeviceIndex) to avoid
			// stale old hand objects that may still report IsTracking=true.
			var hands = handler.InputInterface.GetDevices<Hand>(h => h.Chirality == side);
			if (hands == null || hands.Count == 0) return false;
			Hand newest = hands[0];
			foreach (var h in hands)
				if (h.DeviceIndex > newest.DeviceIndex) newest = h;
			return newest.IsDeviceActive && newest.IsTracking;
		} catch (Exception ex) {
			Msg($"[CyberFingerMod] AreHandsVisible exception ({handler.Side}): {ex.Message}");
			return false;
		}
	}





	// Userspace laser filter states (local-space, Patch 1)
	private static readonly Dictionary<Chirality, SideFilterState> _userspaceFilterStates =
		new Dictionary<Chirality, SideFilterState> {
			{ Chirality.Left,  new SideFilterState() },
			{ Chirality.Right, new SideFilterState() },
		};

	// World-space laser filter states (world-space, Patch 2)
	private static readonly Dictionary<Chirality, SideFilterState> _worldFilterStates =
		new Dictionary<Chirality, SideFilterState> {
			{ Chirality.Left,  new SideFilterState() },
			{ Chirality.Right, new SideFilterState() },
		};

	// Spherical interpolation between two unit vectors.
	// Falls back to normalised lerp near antipodes to avoid NaN.
	private static float3 SlerpDir(in float3 a, in float3 b, float t) {
		float dot = MathX.Clamp(MathX.Dot(a, b), -1f, 1f);
		if (dot > 0.9999f) return MathX.Lerp(a, b, t).Normalized; // nearly same dir
		if (dot < -0.9999f) return MathX.Lerp(a, b, t).Normalized; // antipodal
		float theta    = MathX.Acos(dot);
		float sinTheta = MathX.Sin(theta);
		float wa = MathX.Sin((1f - t) * theta) / sinTheta;
		float wb = MathX.Sin(t * theta)         / sinTheta;
		return (a * wa + b * wb).Normalized;
	}

	// ══════════════════════════════════════════════════════════════════════
	//  FORWARD-ONLY FILTER
	//
	//  Stage 1: sternum-to-hand spatial blend — corrects systematic bias
	//           from body posture, fades out on fast sweeps.
	//  Stage 2: constant-velocity Kalman on spherical coords — handles
	//           correlated lateral jitter on straight sweeps.
	//  Stage 3: velocity-aware dead-band — freezes output completely when
	//           angular speed is below threshold, eliminating residual
	//           jitter when holding still without adding any lag to motion.
	// ══════════════════════════════════════════════════════════════════════
	internal static float3 FilterForward(
			SideFilterState state,
			in float3       worldTip,
			in float3       worldForward,
			in float3       worldSternum,
			double          worldTime) {

		float dt = (state.LastTime < 0.0)
		               ? (1f / 90f)
		               : (float)(worldTime - state.LastTime);
		state.LastTime = worldTime;

		// ── Stage 1: Sternum-to-hand spatial blend (shared) ────────────
		float3 sternumDir = (worldTip - worldSternum).Normalized;
		float3 handDir    = worldForward.Normalized;

		float blendWeight = ArmBlendWeight;
		if (dt > 0f) {
			float dot          = MathX.Clamp(MathX.Dot(handDir, state.PrevForward.Normalized), -1f, 1f);
			float angularSpeed = MathX.Acos(dot) / dt;
			const float slowThresh = 0.087f; // 5 deg/s
			const float fastThresh = 1.047f; // 60 deg/s
			float t = MathX.Clamp01((angularSpeed - slowThresh) / (fastThresh - slowThresh));
			blendWeight = MathX.Lerp(blendWeight, blendWeight * 0.10f, t);
		}
		state.PrevForward = handDir;

		float3 blended = (sternumDir * blendWeight + handDir * (1f - blendWeight)).Normalized;

		// ── Stage 2: UKF temporal filter ───────────────────────────────
		float3 kalmanOut = state.UKFFilter.Filter(in blended, dt,
		                      KalmanProcessNoise, KalmanMeasurementNoise,
		                      KalmanVelocityDamping);

		// ── Stage 3: Velocity-aware dead-band ─────────────────────────
		// When the Kalman output is barely moving, freeze it at the last
		// locked direction so sub-threshold jitter produces zero laser
		// movement. The lock releases smoothly as angular speed rises.
		float dbAngle = DeadBandAngle;
		if (dbAngle <= 0f) return kalmanOut; // dead-band disabled

		if (state.LockedForward == null) {
			// First frame — initialise lock to current output
			state.LockedForward = kalmanOut;
			return kalmanOut;
		}

		float3 locked = state.LockedForward.Value;

		// Angular distance between Kalman output and current lock point
		float cosA       = MathX.Clamp(MathX.Dot(kalmanOut, locked), -1f, 1f);
		float offsetDeg  = MathX.Acos(cosA) * 57.29578f;

		// Angular speed of the Kalman output (smoothed, so this is reliable)
		float kalmanSpeed = 0f;
		if (dt > 0f) {
			float cosS     = MathX.Clamp(MathX.Dot(kalmanOut, state.PrevForward), -1f, 1f);
			kalmanSpeed    = MathX.Acos(cosS) * 57.29578f / dt;
		}

		float releaseSpeed = DeadBandReleaseSpeed;

		// Two conditions to update the lock:
		//   A) Output has drifted outside the dead-band radius — re-centre lock
		//   B) Moving fast enough — blend toward Kalman output proportionally
		if (offsetDeg > dbAngle) {
			// Pull lock toward Kalman output just enough to sit on the boundary,
			// so we don't snap but do follow once the threshold is crossed.
			float pullT = (offsetDeg - dbAngle) / offsetDeg;
			locked = SlerpDir(locked, kalmanOut, pullT);
		}

		// Speed-based blend: 0 at rest → 1 at releaseSpeed
		float speedT = MathX.Clamp01(kalmanSpeed / releaseSpeed);
		if (speedT > 0f) {
			locked = SlerpDir(locked, kalmanOut, speedT);
		}

		state.LockedForward = locked;
		return locked;
	}

	// ══════════════════════════════════════════════════════════════════════
	//  PATCH 1 — UpdateUserspaceToolOffsets  (userspace laser)
	//
	//  Operates in hand-LOCAL space. Estimated elbow = (0, 0.30, 0) in that
	//  local frame (controller local-up points toward shoulder/elbow).
	// ══════════════════════════════════════════════════════════════════════
	[HarmonyPatch(typeof(InteractionHandler), "UpdateUserspaceToolOffsets")]
	public static class InteractionHandler_UpdateUserspaceToolOffsets_Patch {

		private static readonly MethodInfo EndGrabMethod =
			typeof(InteractionHandler).GetMethod("EndGrab", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("InteractionHandler.EndGrab not found");

		private static readonly FieldInfo LaserSlotField =
			typeof(InteractionHandler).GetField("_laserSlot", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException("InteractionHandler._laserSlot not found");

		static bool Prefix(InteractionHandler __instance) {

			bool userspace = __instance.World == Userspace.UserspaceWorld;

			// Non-userspace branch (world-space world): must always run via
			// original — it calls SetWorldControllerData which feeds the pipe
			// for the userspace branch next frame. Never intercept it.
			if (!userspace)
				return true;

			// Userspace branch: we always take over (replaces the original
			// entirely) so we can:
			//   a) optionally suppress the snap-to-raw correction (ForceOverride)
			//   b) optionally apply hand-tracking filtering (ImprovedFiltering)
			// Both are independent — ForceOverride only controls (a), filtering
			// only controls (b).

			Userspace.ControllerData controllerData = Userspace.GetControllerData(__instance.Side);

			controllerData.userspaceController    = __instance;
			controllerData.userspaceHoldingThings = __instance.IsHoldingObjects;
			if (controllerData.worldHoldingThings) {
				EndGrabMethod.Invoke(__instance, null);
			}

			// Replicate original tip/forward selection exactly —
			// start from the engine's interpolated controllerData values,
			// then snap to raw if they've drifted too far.
			float3 localTip2     = __instance.Slot.GlobalPointToLocal(__instance.RawCurrentTip);
			float3 localForward2 = __instance.Slot.GlobalDirectionToLocal(__instance.RawCurrentTipForward);
			float3 targetLocalTip     = controllerData.pointOffset;
			float3 targetLocalForward = controllerData.forward;
			float  num2  = MathX.Distance(in targetLocalTip, in localTip2);
			float  angle = MathX.Angle(in targetLocalForward, in localForward2);
			// ForceOverride=true  → skip correction (preserve custom avatar laser placement)
			// ForceOverride=false → allow correction if drifted (original behaviour)
			if (!CyberFingerMod.ForceOverride && (num2 > 0.3f || angle > 45f)) {
				targetLocalTip     = localTip2;
				targetLocalForward = localForward2;
			}

			// ── Improved filtering ───────────────────────────────────
			// Applied after ForceOverride selection so that avatar-displaced
			// laser tips are respected. We convert targetLocalTip/Forward back
			// to world space for the filter, then back to local.
			if (CyberFingerMod.ImprovedFiltering && CyberFingerMod.FilteringEnabled) {
				float3 worldTip     = __instance.Slot.LocalPointToGlobal(in targetLocalTip);
				float3 worldForward = __instance.Slot.LocalDirectionToGlobal(in targetLocalForward);

				float3 headPos      = __instance.LocalUserRoot?.HeadPosition ?? worldTip;
				float  userScale    = __instance.LocalUserRoot?.GlobalScale ?? 1f;
				float3 worldSternum = new float3(headPos.x, headPos.y + CyberFingerMod.SternumOffset * userScale, headPos.z);

				float3 filteredWorldForward = FilterForward(
					_userspaceFilterStates[__instance.Side],
					in worldTip,
					in worldForward,
					in worldSternum,
					__instance.World.Time.WorldTime);

				targetLocalForward = __instance.Slot.GlobalDirectionToLocal(in filteredWorldForward);
				// targetLocalTip unchanged — no position filtering
			}
			// ── End filtering ─────────────────────────────────────────

			floatQ localRotation = floatQ.LookRotation(in targetLocalForward, float3.Up);

			var laserSlotRef = (SyncRef<Slot>)LaserSlotField.GetValue(__instance)!;
			Slot laserSlot   = laserSlotRef.Target;
			laserSlot.LocalPosition = __instance.Slot.LocalPointToSpace(in targetLocalTip,   laserSlot.Parent);
			laserSlot.LocalRotation = __instance.Slot.LocalRotationToSpace(in localRotation, laserSlot.Parent);
			Userspace.SetUserspaceLaserActive(__instance.Side, __instance.Laser.LaserActive, __instance.Laser.CurrentHit != null);

			return false;
		}
	}

	// ══════════════════════════════════════════════════════════════════════
	//  PATCH 2 — UpdateLaserRoot  (world-space laser)
	//
	//  The engine's original implementation is just two lines:
	//      target.GlobalPosition = CurrentTip;
	//      target.GlobalRotation = floatQ.LookRotation(CurrentTipForward, Up);
	//
	//  We intercept it for the LOCAL user only, and only when hand-tracking
	//  is active (FilteringEnabled=false disables for standard controllers →
	//  original 2-liner runs unmodified, which is correct for 6DOF).
	//  Remote users are guarded by IsOwnedByLocalUser.
	// ══════════════════════════════════════════════════════════════════════
	[HarmonyPatch(typeof(InteractionHandler), "UpdateLaserRoot")]
	public static class InteractionHandler_UpdateLaserRoot_Patch {

		private static readonly FieldInfo LaserSlotField =
			typeof(InteractionHandler).GetField("_laserSlot", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException("InteractionHandler._laserSlot not found");

		static bool Prefix(InteractionHandler __instance) {

			// Only filter for the local user in world-space worlds.
			if (!__instance.IsOwnedByLocalUser || __instance.World == Userspace.UserspaceWorld)
				return true;

			if (!CyberFingerMod.ImprovedFiltering)
				return true;

			if (!CyberFingerMod.FilteringEnabled)
				return true;

			float3 worldTip     = __instance.CurrentTip;
			float3 worldForward = __instance.CurrentTipForward;

			// Sternum anchor: head dropped to chest height, avatar-scale-aware
			float3 headPos      = __instance.LocalUserRoot?.HeadPosition ?? worldTip;
			float  userScale    = __instance.LocalUserRoot?.GlobalScale ?? 1f;
			float3 worldSternum = new float3(headPos.x, headPos.y + CyberFingerMod.SternumOffset * userScale, headPos.z);

			float3 filteredForward = FilterForward(
				_worldFilterStates[__instance.Side],
				in worldTip,
				in worldForward,
				in worldSternum,
				__instance.World.Time.WorldTime);

			// Tip passes through unfiltered — no displacement of laser origin.
			var laserSlotRef = (SyncRef<Slot>)LaserSlotField.GetValue(__instance)!;
			Slot laserSlot   = laserSlotRef.Target;
			laserSlot.GlobalPosition = worldTip;
			laserSlot.GlobalRotation = floatQ.LookRotation(in filteredForward, float3.Up);

			return false;
		}

	}

	// ══════════════════════════════════════════════════════════════════════
	//  PATCH 3 — RawDataTool.UpdateValues  (gamepad support — unchanged)
	// ══════════════════════════════════════════════════════════════════════
	[HarmonyPatch(typeof(RawDataTool), "UpdateValues")]
	public static class RawDataTool_UpdateValues_Patch {
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<float>>>  PrimaryStrengthStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<float>>>("_primaryStrengthStream");
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<float2>>> SecondaryAxisStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<float2>>>("_secondaryAxisStream");
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<bool>>>   PrimaryStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<bool>>>("_primaryStream");
		static readonly AccessTools.FieldRef<RawDataTool, SyncRef<ValueStream<bool>>>   SecondaryStreamRef =
			AccessTools.FieldRefAccess<RawDataTool, SyncRef<ValueStream<bool>>>("_secondaryStream");

		static readonly AccessTools.FieldRef<RawDataTool, Sync<float>>  RawStrengthRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<float>>("_rawStrength");
		static readonly AccessTools.FieldRef<RawDataTool, Sync<float2>> RawAxisRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<float2>>("_rawAxis");
		static readonly AccessTools.FieldRef<RawDataTool, Sync<bool>>   RawPrimaryRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<bool>>("_rawPrimary");
		static readonly AccessTools.FieldRef<RawDataTool, Sync<bool>>   RawSecondaryRef =
			AccessTools.FieldRefAccess<RawDataTool, Sync<bool>>("_rawSecondary");

		static readonly System.Reflection.FieldInfo GMField =
			AccessTools.Field(typeof(InputInterface), "_gamepadManager");
		static readonly System.Reflection.FieldInfo GamepadsField =
			AccessTools.Field(typeof(GamepadManager), "_gamepads");

		static bool Prefix(RawDataTool __instance) {
			var worker = (Worker)__instance;
			var world  = worker.World;
			var ah     = ((Tool)__instance).ActiveHandler;

			bool primaryBlocked   = ah?.BlockPrimary   ?? false;
			bool secondaryBlocked = ah?.BlockSecondary ?? false;

			var strengthStream  = PrimaryStrengthStreamRef(__instance).Target;
			var axisStream      = SecondaryAxisStreamRef(__instance).Target;
			var primaryStream   = PrimaryStreamRef(__instance).Target;
			var secondaryStream = SecondaryStreamRef(__instance).Target;

			float  ctrlStrength  = strengthStream  != null ? strengthStream.Value  : RawStrengthRef(__instance).Value;
			float2 ctrlAxis      = axisStream      != null ? axisStream.Value      : RawAxisRef(__instance).Value;
			bool   ctrlPrimary   = primaryStream   != null ? primaryStream.Value   : RawPrimaryRef(__instance).Value;
			bool   ctrlSecondary = secondaryStream != null ? secondaryStream.Value : RawSecondaryRef(__instance).Value;

			float  gpStrength  = 0f;
			float2 gpAxis      = float2.Zero;
			bool   gpPrimary   = false;
			bool   gpSecondary = false;

			var inputInterface = worker.InputInterface;
			var gm   = (GamepadManager)GMField.GetValue(inputInterface);
			var dict = gm != null ? (Dictionary<string, StandardGamepad>)GamepadsField.GetValue(gm) : null;
			var gamepad = dict?.Values.FirstOrDefault();

			if (gamepad != null && (__instance.Equipped?.Value ?? false)) {
				var side = ah?.Side ?? Chirality.Left;

				Analog2D axisProp;
				Digital  primaryProp;
				Digital  secondaryProp;

				if (side == Chirality.Left) {
					axisProp      = gamepad.LeftThumbstick;
					primaryProp   = gamepad.A;
					secondaryProp = gamepad.LeftThumbstickClick;
				} else {
					axisProp      = gamepad.RightThumbstick;
					primaryProp   = gamepad.X;
					secondaryProp = gamepad.RightThumbstickClick;
				}

				var gpAxisStream      = axisProp.GetStream(world);
				var gpPrimaryStream   = primaryProp.GetStream(world);
				var gpSecondaryStream = secondaryProp.GetStream(world);

				if (gpAxisStream      != null) gpAxis      = gpAxisStream.Value;
				if (gpPrimaryStream   != null) gpPrimary   = gpPrimaryStream.Value;
				if (gpSecondaryStream != null) gpSecondary = gpSecondaryStream.Value;

				gpStrength = gpPrimary ? 1f : 0f;
			}

			float  mergedStrength  = gpStrength;
			float2 mergedAxis      = secondaryBlocked ? float2.Zero : ClampAxis(ctrlAxis + gpAxis);
			bool   mergedSecondary = !secondaryBlocked && (ctrlSecondary || gpSecondary);
			bool   mergedPrimary   = gpPrimary;

			__instance.PrimaryStrength.Value = mergedStrength;
			__instance.SecondaryAxis.Value   = mergedAxis;
			__instance.Primary.Value         = mergedPrimary;
			__instance.Secondary.Value       = mergedSecondary;

			return false;
		}

		static float  Clamp01(float x)    => x < 0f ? 0f : x > 1f ? 1f : x;
		static float2 ClampAxis(float2 v) {
			float lenSq = v.x * v.x + v.y * v.y;
			if (lenSq > 1f) { float inv = 1f / (float)Math.Sqrt(lenSq); return new float2(v.x * inv, v.y * inv); }
			return v;
		}
		static float ClampMinus1To1(float x) => x < -1f ? -1f : x > 1f ? 1f : x;
	}

	// ══════════════════════════════════════════════════════════════════════
	//  PATCH 4 — VirtualKeyboard.IsShown  (unchanged)
	// ══════════════════════════════════════════════════════════════════════
	[HarmonyPatch(typeof(VirtualKeyboard), "IsShown", MethodType.Setter)]
	static class VK_IsShown_Set_Patch {
		static bool Prefix(ref bool value) {
			if (!HideVirtualKeyboard) return true;
			value = false;
			return true;
		}
	}

	// ══════════════════════════════════════════════════════════════════════
	//  PATCH 5 — StandardGamepad.Bind  (unchanged)
	// ══════════════════════════════════════════════════════════════════════
	[HarmonyPatch(typeof(StandardGamepad), "Bind")]
	public static class StandardGamepad_Bind_Patch {

		static private ScreenLocomotionDirection GenerateScreenDirection(StandardGamepad __instance) {
			ScreenLocomotionDirection dir = new ScreenLocomotionDirection();
			var rs   = InputNode.Analog2D(__instance.RightThumbstick);
			var ls   = InputNode.Analog2D(__instance.LeftThumbstick);
			var ySum = new SumInputs<float>();
			ySum.Inputs.Add(rs.Y());
			ySum.Inputs.Add(ls.Y());
			dir.Axis = InputNode.XY(ls.X(), ySum);
			dir.Up   = InputNode.Digital(__instance.RightThumbstickClick);
			dir.Down = InputNode.Digital(__instance.LeftThumbstickClick);
			return dir;
		}

		static private VR_SingleLocomotionTurn GenerateTurn(StandardGamepad __instance) =>
			new VR_SingleLocomotionTurn(InputNode.Analog2D(__instance.RightThumbstick).X());

		static private VR_SingleLocomotionTurn GenerateTurnLeft(StandardGamepad __instance) =>
			new VR_SingleLocomotionTurn(InputNode.Analog2D(__instance.LeftThumbstick).X());

		static bool Prefix(StandardGamepad __instance, InputGroup group) {

			if (group is SmoothThreeAxisLocomotionInputs threeAxisLocomotion) {
				threeAxisLocomotion.Move.AddBinding(GenerateScreenDirection(__instance), __instance);
				threeAxisLocomotion.Jump.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
				threeAxisLocomotion.Align.AddBinding(InputNode.Digital(__instance.B), __instance);
				threeAxisLocomotion.Align.AddBinding(InputNode.Digital(__instance.Y), __instance);
				var rs      = InputNode.Analog2D(__instance.RightThumbstick);
				var pitch   = rs.Y().Negate();
				var yaw     = rs.X();
				var right   = InputNode.Digital(__instance.X).ToAnalog();
				var left    = InputNode.Digital(__instance.A).ToAnalog();
				var leftNeg = left.Negate();
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
					teleport.Backstep.AddBinding(InputNode.Analog2D(__instance.LeftThumbstick).Y().Negate().ToDigital(0.8f), __instance);
					teleport.TurnDelta.AddBinding(GenerateTurnLeft(__instance), __instance);
				}
				if (teleport.Side == Chirality.Right) {
					teleport.Teleport.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
					teleport.Backstep.AddBinding(InputNode.Analog2D(__instance.RightThumbstick).Y().Negate().ToDigital(0.8f), __instance);
					teleport.TurnDelta.AddBinding(GenerateTurn(__instance), __instance);
				}
			}

			if (group is DevToolInputs devTool) {
				if (devTool.Side == Chirality.Left) {
					Msg("Added Left DevToolInputs");
					devTool.Focus.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance, null, 20000);
					devTool.Inspector.AddBinding(InputNode.Digital(__instance.X), __instance, null, 20000);
					devTool.Create.AddBinding(InputNode.Digital(__instance.Y), __instance, null, 20000);
				}
				if (devTool.Side == Chirality.Right) {
					Msg("Added Right DevToolInputs");
					devTool.Focus.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance, null, 20000);
					devTool.Inspector.AddBinding(InputNode.Digital(__instance.A), __instance, null, 20000);
					devTool.Create.AddBinding(InputNode.Digital(__instance.B), __instance, null, 20000);
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
					commonTool.Strength.AddBinding(InputNode.Digital(__instance.X).ToAnalog(), __instance);
					commonTool.Interact.AddBinding(InputNode.Digital(__instance.X), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.Y), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.Y).TapToggle(), __instance);
					commonTool.Menu.AddBinding(InputNode.Digital(__instance.LeftBumper), __instance);
					commonTool.Secondary.AddBinding(InputNode.Digital(__instance.LeftThumbstickClick), __instance);
				}
				if (commonTool.Side == Chirality.Right) {
					Msg("Added Right InteractionHandlerInputs");
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.B), __instance);
					commonTool.Grab.AddBinding(InputNode.Digital(__instance.B).TapToggle(), __instance);
					commonTool.Interact.AddBinding(InputNode.Digital(__instance.A), __instance);
					commonTool.Strength.AddBinding(InputNode.Digital(__instance.A).ToAnalog(), __instance);
					commonTool.Menu.AddBinding(InputNode.Digital(__instance.RightBumper), __instance);
					commonTool.Secondary.AddBinding(InputNode.Digital(__instance.RightThumbstickClick), __instance);
				}
			}

			if (group is GlobalActions globalActions) {
				globalActions.ToggleDash.AddBinding(InputNode.Digital(__instance.Start), __instance);
				globalActions.ToggleDash.AddBinding(InputNode.Digital(__instance.Menu), __instance);
			}

			return false;
		}
	}
}
