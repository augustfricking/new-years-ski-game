using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// A body pose as joint angles in degrees. Positive "Fwd" swings a limb forward, "Out" swings it
    /// away from the body, bends are positive. Poses blend with <see cref="Lerp"/>.
    /// </summary>
    public struct Pose
    {
        public float ThighL, KneeL, ThighR, KneeR;   // thigh forward, knee bend
        public float LegOut, FootIn;                 // legs apart, toes turned in (snowplough)
        public float TorsoPitch, TorsoYaw, TorsoRoll;
        public float HeadYaw, HeadPitch, HeadRoll;         // HeadPitch: negative looks up
        public float ArmFwdL, ArmOutL, ElbowL, ArmFwdR, ArmOutR, ElbowR;
        public float ArmTwistL, ArmTwistR;           // rotates the bent forearm inward (+) across the body
        public float Seated;                         // 1 = root is the seat, legs hang from it
        public float HipDrop;                        // extra metres down (sitting in the snow)

        public static Pose Lerp(in Pose a, in Pose b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Pose
            {
                ThighL = Mathf.Lerp(a.ThighL, b.ThighL, t), KneeL = Mathf.Lerp(a.KneeL, b.KneeL, t),
                ThighR = Mathf.Lerp(a.ThighR, b.ThighR, t), KneeR = Mathf.Lerp(a.KneeR, b.KneeR, t),
                LegOut = Mathf.Lerp(a.LegOut, b.LegOut, t), FootIn = Mathf.Lerp(a.FootIn, b.FootIn, t),
                TorsoPitch = Mathf.Lerp(a.TorsoPitch, b.TorsoPitch, t), TorsoYaw = Mathf.Lerp(a.TorsoYaw, b.TorsoYaw, t), TorsoRoll = Mathf.Lerp(a.TorsoRoll, b.TorsoRoll, t),
                HeadYaw = Mathf.Lerp(a.HeadYaw, b.HeadYaw, t), HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t), HeadRoll = Mathf.Lerp(a.HeadRoll, b.HeadRoll, t),
                ArmFwdL = Mathf.Lerp(a.ArmFwdL, b.ArmFwdL, t), ArmOutL = Mathf.Lerp(a.ArmOutL, b.ArmOutL, t), ElbowL = Mathf.Lerp(a.ElbowL, b.ElbowL, t),
                ArmFwdR = Mathf.Lerp(a.ArmFwdR, b.ArmFwdR, t), ArmOutR = Mathf.Lerp(a.ArmOutR, b.ArmOutR, t), ElbowR = Mathf.Lerp(a.ElbowR, b.ElbowR, t),
                ArmTwistL = Mathf.Lerp(a.ArmTwistL, b.ArmTwistL, t), ArmTwistR = Mathf.Lerp(a.ArmTwistR, b.ArmTwistR, t),
                Seated = Mathf.Lerp(a.Seated, b.Seated, t), HipDrop = Mathf.Lerp(a.HipDrop, b.HipDrop, t),
            };
        }

        public Pose WithArms(float fwdL, float outL, float elbowL, float fwdR, float outR, float elbowR)
        {
            var p = this;
            p.ArmFwdL = fwdL; p.ArmOutL = outL; p.ElbowL = elbowL; p.ArmFwdR = fwdR; p.ArmOutR = outR; p.ElbowR = elbowR;
            return p;
        }

        // ---- library -----------------------------------------------------------------------

        public static readonly Pose Stand = new Pose
        {
            ThighL = 4, KneeL = 8, ThighR = 4, KneeR = 8, LegOut = 4,
            ArmFwdL = 4, ArmOutL = 10, ElbowL = 14, ArmFwdR = 4, ArmOutR = 10, ElbowR = 14,
        };

        public static readonly Pose Ski = new Pose
        {
            ThighL = 28, KneeL = 56, ThighR = 28, KneeR = 56, LegOut = 5, TorsoPitch = 22, HeadPitch = -14,
            ArmFwdL = 38, ArmOutL = 22, ElbowL = 55, ArmFwdR = 38, ArmOutR = 22, ElbowR = 55,
        };

        public static readonly Pose Plough = new Pose
        {
            ThighL = 30, KneeL = 60, ThighR = 30, KneeR = 60, LegOut = 17, FootIn = 20, TorsoPitch = 18, HeadPitch = -10,
            ArmFwdL = 45, ArmOutL = 35, ElbowL = 40, ArmFwdR = 45, ArmOutR = 35, ElbowR = 40,
        };

        public static readonly Pose Tuck = new Pose
        {
            ThighL = 62, KneeL = 124, ThighR = 62, KneeR = 124, LegOut = 4, TorsoPitch = 62, HeadPitch = -48,
            ArmFwdL = 78, ArmOutL = 4, ElbowL = 118, ArmFwdR = 78, ArmOutR = 4, ElbowR = 118, ArmTwistL = 20, ArmTwistR = 20,
        };

        /// <summary>Arms up, screaming straight down the hill.</summary>
        public static readonly Pose Bomber = new Pose
        {
            ThighL = 20, KneeL = 40, ThighR = 20, KneeR = 40, LegOut = 12, TorsoPitch = 8, HeadPitch = 6,
            ArmFwdL = 30, ArmOutL = 120, ElbowL = 20, ArmFwdR = 30, ArmOutR = 120, ElbowR = 20,
        };

        public static readonly Pose Sit = new Pose
        {
            Seated = 1, ThighL = 88, KneeL = 80, ThighR = 88, KneeR = 80, LegOut = 6, TorsoPitch = -4,
            ArmFwdL = 28, ArmOutL = 8, ElbowL = 72, ArmFwdR = 28, ArmOutR = 8, ElbowR = 72, ArmTwistL = 15, ArmTwistR = 15,
        };

        /// <summary>Sitting in the snow after a fall.</summary>
        public static readonly Pose Fallen = new Pose
        {
            Seated = 1, HipDrop = 0f, ThighL = 70, KneeL = 30, ThighR = 84, KneeR = 55, LegOut = 22, TorsoPitch = -18, HeadPitch = 12,
            ArmFwdL = -30, ArmOutL = 30, ElbowL = 20, ArmFwdR = -25, ArmOutR = 34, ElbowR = 25,
        };

        public static readonly Pose Cheer = new Pose
        {
            ThighL = 6, KneeL = 12, ThighR = 6, KneeR = 12, LegOut = 6, TorsoPitch = -6, HeadPitch = -12,
            ArmFwdL = 20, ArmOutL = 150, ElbowL = 25, ArmFwdR = 20, ArmOutR = 150, ElbowR = 25,
        };

        /// <summary>Right hand raised to the mouth, holding a glass.</summary>
        public static readonly Pose Drink = new Pose
        {
            ThighL = 4, KneeL = 8, ThighR = 4, KneeR = 8, LegOut = 5, TorsoPitch = -8, HeadPitch = -16,
            ArmFwdL = 6, ArmOutL = 12, ElbowL = 20, ArmFwdR = 62, ArmOutR = 10, ElbowR = 128, ArmTwistR = 30,
        };

        /// <summary>Right hand up at the goggles.</summary>
        public static readonly Pose SitAdjustGoggles = new Pose
        {
            Seated = 1, ThighL = 88, KneeL = 80, ThighR = 88, KneeR = 80, LegOut = 6, TorsoPitch = -2, HeadPitch = -6,
            ArmFwdL = 28, ArmOutL = 8, ElbowL = 72, ArmFwdR = 80, ArmOutR = 22, ElbowR = 140, ArmTwistL = 15, ArmTwistR = 38,
        };

        /// <summary>Both hands at the collar, zipping the jacket.</summary>
        public static readonly Pose SitZipJacket = new Pose
        {
            Seated = 1, ThighL = 88, KneeL = 80, ThighR = 88, KneeR = 80, LegOut = 6, TorsoPitch = 2, HeadPitch = 20,
            ArmFwdL = 46, ArmOutL = 14, ElbowL = 118, ArmFwdR = 50, ArmOutR = 14, ElbowR = 126, ArmTwistL = 52, ArmTwistR = 52,
        };

        /// <summary>Leaning to the left, pointing with the left arm.</summary>
        public static readonly Pose SitPoint = new Pose
        {
            Seated = 1, ThighL = 88, KneeL = 80, ThighR = 88, KneeR = 80, LegOut = 6, TorsoPitch = 8, TorsoYaw = -14, HeadYaw = -28,
            ArmFwdL = 76, ArmOutL = 34, ElbowL = 8, ArmFwdR = 28, ArmOutR = 8, ElbowR = 72, ArmTwistR = 15,
        };

        /// <summary>Hunched against the wind, arms hugging the body.</summary>
        public static readonly Pose SitCold = new Pose
        {
            Seated = 1, ThighL = 92, KneeL = 86, ThighR = 92, KneeR = 86, LegOut = 2, TorsoPitch = 20, HeadPitch = 12,
            ArmFwdL = 36, ArmOutL = 2, ElbowL = 112, ArmFwdR = 36, ArmOutR = 2, ElbowR = 112, ArmTwistL = 62, ArmTwistR = 62,
        };

        /// <summary>Gripping the safety bar, leaning back, alarmed.</summary>
        public static readonly Pose SitPanic = new Pose
        {
            Seated = 1, ThighL = 84, KneeL = 70, ThighR = 84, KneeR = 70, LegOut = 10, TorsoPitch = -12, HeadPitch = -6,
            ArmFwdL = 64, ArmOutL = 16, ElbowL = 40, ArmFwdR = 64, ArmOutR = 16, ElbowR = 40,
        };
    }
}
