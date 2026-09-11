using UnityEngine;
using System.Collections.Generic;
using Nakatetsu.Train.Brake.ControlDevice;

namespace Nakatetsu.Train.Simulation
{
    public sealed class TrainSimulationController : MonoBehaviour
    {
        // [SerializeField] private List<VvvfController> vvvfControllers;
        [SerializeField] private List<BrakeControlDevice> brakeControllers;


        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            
        }
    }
}