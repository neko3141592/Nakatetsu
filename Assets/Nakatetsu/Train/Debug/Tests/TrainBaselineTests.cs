using System.Collections;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Simulation.Physics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nakatetsu.Train.Debugging.Tests
{
    public sealed class TrainBaselineTests
    {
        [UnityTest]
        public IEnumerator NtLineCanAccelerateAndStopWithServiceBrake()
        {
            // 保存済みSceneと通常のUpdate経路を使い、物理StateやBus値は直接変更しない。
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NtLine.unity", LoadSceneMode.Single);
            yield return null;

            var controls = Object.FindObjectsByType<TrainDebugController>(FindObjectsSortMode.None);
            Assert.That(controls, Has.Length.EqualTo(1));
            var control = controls[0];
            var train = control.GetComponent<TrainRoot>();
            Assert.That(train, Is.Not.Null);
            Assert.That(train.ConsistDefinition.CarCount, Is.EqualTo(10));
            var physics = train.GetComponentInChildren<TrainPhysicsController>();
            var brake = train.GetComponentInChildren<TimsBrakeController>();
            Assert.That(physics, Is.Not.Null);
            Assert.That(brake, Is.Not.Null);

            control.PrepareFrontCab();
            yield return new WaitForSeconds(2f);
            Assert.That(brake.Output.hasCommands, Is.True, control.GetStatus());
            Assert.That(brake.Output.isEmergency, Is.False, control.GetStatus());
            Assert.That(physics.Context.Output.signedVelocityMps, Is.EqualTo(0f).Within(0.01f));
            TestContext.WriteLine("Prepared\n" + control.GetStatus());

            control.SetPower(control.MaxPowerPosition);
            yield return new WaitForSeconds(8f);
            float poweredSpeedMps = physics.Context.Output.signedVelocityMps;
            Assert.That(poweredSpeedMps, Is.GreaterThan(1f), control.GetStatus());
            Assert.That(physics.Context.Input.totalTractionForceN, Is.GreaterThan(0f));
            Assert.That(brake.Output.isEmergency, Is.False, control.GetStatus());
            TestContext.WriteLine("Powered\n" + control.GetStatus());

            control.SetBrake(control.MaxBrakePosition);
            float elapsedSeconds = 0f;
            float peakPressureKPa = 0f;
            float peakBrakeForceN = 0f;
            while (elapsedSeconds < 20f)
            {
                yield return null;
                elapsedSeconds += Time.deltaTime;
                peakBrakeForceN = Mathf.Max(peakBrakeForceN, physics.Context.Input.totalBrakeForceN);
                foreach (var car in brake.Context.Input.cars)
                    peakPressureKPa = Mathf.Max(peakPressureKPa, car.bcPressureKPa);
                Assert.That(brake.Output.isEmergency, Is.False, control.GetStatus());
                Assert.That(physics.Context.Output.signedVelocityMps, Is.GreaterThanOrEqualTo(-0.01f));
                if (physics.Context.Output.signedVelocityMps < 0.01f) break;
            }

            TestContext.WriteLine($"Braking: start={poweredSpeedMps:F4} m/s, duration={elapsedSeconds:F3} s, " +
                $"peak BC={peakPressureKPa:F3} kPa, peak air brake={peakBrakeForceN:F1} N\n" + control.GetStatus());
            Assert.That(peakPressureKPa, Is.GreaterThan(0f));
            Assert.That(peakBrakeForceN, Is.GreaterThan(0f));
            Assert.That(physics.Context.Output.signedVelocityMps, Is.EqualTo(0f).Within(0.01f), control.GetStatus());
            yield return new WaitForSeconds(1f);
            Assert.That(physics.Context.Output.signedVelocityMps, Is.EqualTo(0f).Within(0.01f));
        }
    }
}
