using System.Collections.Generic;
using Nakatetsu.Train.Simulation.Orchestration;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.LoadWeightDevice
{
    public sealed class LoadWeightDeviceController
    {
        [SerializeField] private float tickDurationSeconds = 0.02f;
        [SerializeField] private List<TrainSimulationController> trains;

        private void Start()
        {
            
        }

        private void Tick()
        {
            foreach (TrainSimulationController train in trains)
            {
                
            }
        }
    } 
}
