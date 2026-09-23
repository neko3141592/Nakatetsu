using System.Collections.Generic;
using Nakatetsu.Train.Simulation.Orchestration;
using UnityEngine;

namespace Nakatetsu.Application.Simulation
{
    public sealed class ApplicationSimulationController
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