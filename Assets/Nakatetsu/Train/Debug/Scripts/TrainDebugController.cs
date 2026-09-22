using System.Text;
using Nakatetsu.Train.Equipment.Operation;
using Nakatetsu.Train.Equipment.Operation.CabActivationSwitch;
using Nakatetsu.Train.Equipment.Safety.Eb;
using Nakatetsu.Train.Equipment.Shared;
using Nakatetsu.Train.Equipment.Tims.Brake;
using Nakatetsu.Train.Equipment.Tims;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Door;
using Nakatetsu.Train.Equipment.Tims.Operation;
using Nakatetsu.Train.Simulation.Door;
using Nakatetsu.Train.Simulation.Orchestration;
using Nakatetsu.Train.Simulation.Physics;
using UnityEngine;

namespace Nakatetsu.Train.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Train/Debug/Train Debug Controller")]
    public sealed class TrainDebugController : MonoBehaviour
    {
        [SerializeField] private TrainRoot trainRoot;
        private TrainSimulationController simulation;
        private TrainPhysicsController physics;
        private TimsBrakeController brake;
        private TimsDirectionController direction;
        private TimsCommunicationController communication;
        private MasterController frontMaster, rearMaster;
        private CabActivationSwitchController frontSwitch, rearSwitch;
        private EbDevice[] ebDevices = new EbDevice[0];
        private string lastOperation = "運転台を選択してください。";

        public int MaxPowerPosition => ActiveMaster != null ? ActiveMaster.MaxPowerPosition : 4;
        public int MaxBrakePosition => ActiveMaster != null ? ActiveMaster.MaxServiceBrakePosition : 7;
        public string LastOperation => lastOperation;
        private MasterController ActiveMaster => direction == null ? null :
            direction.Output.activatedCabPosition == ActivatedCabPosition.Front ? frontMaster :
            direction.Output.activatedCabPosition == ActivatedCabPosition.Rear ? rearMaster : null;

        private void Start() => RefreshReferences();

        [ContextMenu("機器を再取得")]
        public void RefreshReferences()
        {
            if (trainRoot == null) trainRoot = GetComponentInParent<TrainRoot>();
            frontMaster = rearMaster = null;
            frontSwitch = rearSwitch = null;
            simulation = null;
            physics = null;
            brake = null;
            direction = null;
            communication = null;
            ebDevices = new EbDevice[0];
            if (trainRoot == null) { lastOperation = "TrainRootのある編成ルートに配置してください。"; return; }

            simulation = trainRoot.GetComponentInChildren<TrainSimulationController>(true);
            physics = trainRoot.GetComponentInChildren<TrainPhysicsController>(true);
            brake = trainRoot.GetComponentInChildren<TimsBrakeController>(true);
            direction = trainRoot.GetComponentInChildren<TimsDirectionController>(true);
            communication = trainRoot.GetComponentInChildren<TimsCommunicationController>(true);
            ebDevices = trainRoot.GetComponentsInChildren<EbDevice>(true);
            int rearIndex = trainRoot.ConsistDefinition != null ? trainRoot.ConsistDefinition.CarCount - 1 : -1;
            foreach (MasterController master in trainRoot.GetComponentsInChildren<MasterController>(true))
            {
                if (master.AssignedCarIndex == 0) frontMaster = master;
                if (master.AssignedCarIndex == rearIndex) rearMaster = master;
            }
            foreach (CabActivationSwitchController cab in trainRoot.GetComponentsInChildren<CabActivationSwitchController>(true))
            {
                var assignment = cab.GetComponent<TrainEquipmentAssignment>();
                if (assignment == null || !assignment.IsAssigned) continue;
                if (assignment.AssignedCarIndex == 0) frontSwitch = cab;
                if (assignment.AssignedCarIndex == rearIndex) rearSwitch = cab;
            }
            if (UnityEngine.Application.isPlaying && simulation != null) simulation.ResolveReferences();
            lastOperation = "機器を再取得しました。";
        }

        [ContextMenu("前側運転台を準備")]
        public void PrepareFrontCab() => PrepareCab(false);
        [ContextMenu("後側運転台を準備")]
        public void PrepareRearCab() => PrepareCab(true);

        private void PrepareCab(bool rear)
        {
            if (!UnityEngine.Application.isPlaying) return;
            RefreshReferences();
            if (physics == null || simulation == null || brake == null || direction == null ||
                frontMaster == null || rearMaster == null || frontSwitch == null || rearSwitch == null ||
                frontSwitch == rearSwitch)
            { lastOperation = "編成定義、前後の操作機器、TIMS、Simulation、Physicsの配置を確認してください。"; return; }
            if (Mathf.Abs(physics.Context.Output.signedVelocityMps) >= 0.01f)
            { lastOperation = "運転台の切り替えは停車後に行ってください。"; return; }
            frontMaster.SetInputEnabled(true);
            rearMaster.SetInputEnabled(true);
            frontMaster.SetNeutral();
            rearMaster.SetNeutral();
            frontMaster.SetReverserPosition(ReverserPosition.Neutral);
            rearMaster.SetReverserPosition(ReverserPosition.Neutral);
            frontSwitch.SetPosition(rear ? CabActivationPosition.Rear : CabActivationPosition.Front);
            rearSwitch.SetPosition(rear ? CabActivationPosition.Front : CabActivationPosition.Rear);
            (rear ? rearMaster : frontMaster).SetReverserPosition(ReverserPosition.Forward);
            lastOperation = (rear ? "後側" : "前側") + "運転台を準備しました。TIMSの更新後にPノッチを操作してください。";
        }

        public void SetPower(int notch)
        {
            if (!TryGetMaster(out MasterController master)) return;
            if (brake == null || brake.Output.isEmergency)
            { lastOperation = "非常制動中です。停車・Nで原因を解除してから力行してください。"; return; }
            master.SetPowerPosition(Mathf.Clamp(notch, 0, master.MaxPowerPosition));
            lastOperation = $"力行 P{master.PowerPosition}";
        }

        public void SetBrake(int notch)
        {
            if (!TryGetMaster(out MasterController master)) return;
            master.SetBrakePosition(Mathf.Clamp(notch, 0, master.MaxServiceBrakePosition));
            lastOperation = $"常用 B{master.BrakePosition}";
        }

        [ContextMenu("ノッチ N")]
        public void SetNeutral()
        {
            if (!TryGetMaster(out MasterController master)) return;
            master.SetNeutral();
            lastOperation = "Nにしました。非常保持は停車・原因解消後にTIMSが解除します。";
        }

        [ContextMenu("非常ブレーキ")]
        public void SetEmergencyBrake()
        {
            if (!TryGetMaster(out MasterController master)) return;
            master.SetEmergencyBrake();
            lastOperation = "非常ノッチを投入しました。";
        }

        public void SetReverser(int position)
        {
            if (!TryGetMaster(out MasterController master)) return;
            if (physics == null || Mathf.Abs(physics.Context.Output.signedVelocityMps) >= 0.01f)
            { lastOperation = "レバーサー操作は停車後に行ってください。"; return; }
            lastOperation = master.SetReverserPosition((ReverserPosition)Mathf.Clamp(position, -1, 1))
                ? "レバーサーを変更しました。" : "先にノッチをNにしてください。";
        }

        private bool TryGetMaster(out MasterController master)
        {
            master = ActiveMaster;
            if (!UnityEngine.Application.isPlaying) { lastOperation = "Playモードで操作してください。"; return false; }
            if (master == null) { lastOperation = "先に運転台を準備してください。"; return false; }
            return true;
        }

        [ContextMenu("ドア/左側を開く（編成基準）")]
        public void OpenLeftDoors() => RequestDoors(true, DoorMotionCommand.Open);

        [ContextMenu("ドア/右側を開く（編成基準）")]
        public void OpenRightDoors() => RequestDoors(false, DoorMotionCommand.Open);

        [ContextMenu("ドア/両側を閉じる")]
        public void CloseDoors() => RequestDoors(null, DoorMotionCommand.Close);

        private void RequestDoors(bool? leftSide, DoorMotionCommand command)
        {
            if (!UnityEngine.Application.isPlaying) { lastOperation = "Playモードで操作してください。"; return; }
            if (communication == null) RefreshReferences();
            var doors = communication != null ? communication.GetComponent<TimsDoorController>() : null;
            if (communication == null || !communication.isActiveAndEnabled || doors == null || !doors.isActiveAndEnabled)
            { lastOperation = "TIMS通信・ドア制御の配置と有効状態を確認してください。"; return; }

            bool accepted;
            if (leftSide.HasValue) accepted = doors.RequestAll(leftSide.Value, command);
            else
            {
                bool leftAccepted = doors.RequestAll(true, command);
                bool rightAccepted = doors.RequestAll(false, command);
                accepted = leftAccepted && rightAccepted;
            }
            if (!accepted) { lastOperation = "ドア指令を送信できませんでした。編成定義と各車のTIMS Busを確認してください。"; return; }
            lastOperation = leftSide.HasValue
                ? $"全車の{(leftSide.Value ? "左" : "右")}側へ開指令を送信しました（編成基準）。速度・開扉許可により開かない場合があります。"
                : "全車の両側へ閉指令を送信しました。";
        }

        private void AppendDoorStatus(StringBuilder text)
        {
            var doors = communication != null ? communication.GetComponent<TimsDoorController>() : null;
            if (communication == null || !communication.isActiveAndEnabled || doors == null || !doors.isActiveAndEnabled)
            { text.AppendLine("TIMS Door: 未取得または無効"); return; }
            var bus = communication.MasterBus;
            bool valid = bus.TryGetBool(TimsDoorController.HasValidStateKey, out bool v) && v;
            string closed = valid && bus.TryGetBool(TimsDoorController.AllClosedKey, out bool c) ? (c ? "全閉" : "未閉") : "不明";
            string traction = bus.TryGetBool(TimsDoorController.TractionPermittedKey, out bool p) ? (p ? "許可" : "禁止") : "不明";
            text.AppendLine($"ドア: {closed} / 力行: {traction}（左右は編成基準）");
            int count = trainRoot != null && trainRoot.ConsistDefinition != null ? trainRoot.ConsistDefinition.CarCount : 0;
            for (int i = 0; i < count; i++)
            {
                communication.TryGetLocalBus(i, out TimsBusState local);
                text.AppendLine($"車両{i + 1} ドア: 左 [{GetDoorStates(local, true)}] / 右 [{GetDoorStates(local, false)}]");
            }
        }

        private static string GetDoorStates(TimsBusState bus, bool leftSide)
        {
            if (bus == null || !bus.TryGetIntArray(leftSide ? DoorTimsBusSource.LeftStatusKey : DoorTimsBusSource.RightStatusKey, out int[] states))
                return "不明";
            var labels = new string[states.Length];
            for (int i = 0; i < states.Length; i++)
                labels[i] = ((DoorStatus)states[i]) switch
                {
                    DoorStatus.Closed => "閉",
                    DoorStatus.Opening => "開動作中",
                    DoorStatus.Open => "開",
                    DoorStatus.Closing => "閉動作中",
                    DoorStatus.Stopped => "停止",
                    DoorStatus.Fault => "故障",
                    _ => "不明"
                };
            return string.Join(" / ", labels);
        }

        public string GetStatus()
        {
            var text = new StringBuilder();
            text.AppendLine(physics != null
                ? $"速度: {physics.Context.Output.signedVelocityMps * 3.6f:F2} km/h（編成前方＋）"
                : "Physics: 未取得");
            var control = brake != null ? brake.GetComponent<TimsControlController>() : null;
            text.AppendLine(control == null ? "TIMS制御: 未配置" :
                !control.isActiveAndEnabled ? "TIMS制御: 無効" : $"非常理由: {control.EmergencyReason}");
            if (simulation == null || !simulation.isActiveAndEnabled) text.AppendLine("Simulation: 未配置または無効");
            var master = ActiveMaster;
            text.AppendLine(direction != null ? $"有効運転台: {direction.Output.activatedCabPosition}" : "TIMS Direction: 未取得");
            text.AppendLine(master != null ? $"P{master.PowerPosition} / B{master.BrakePosition} / レバーサー {master.ReverserPosition}" : "マスコン: 未選択");
            text.AppendLine(brake != null ? $"TIMS非常: {brake.Output.isEmergency} / 指令有効: {brake.Output.hasCommands}" : "TIMS Brake: 未取得");
            AppendDoorStatus(text);
            if (brake != null)
                for (int i = 0; i < brake.Context.Input.cars.Count; i++)
                {
                    var car = brake.Context.Input.cars[i];
                    if (car == null) continue;
                    text.AppendLine($"車両{i + 1}: 回生 {car.regenForceN:F0} / 能力 {car.regenCapN:F0} N、BC {car.bcPressureKPa:F1} kPa");
                    if (i < brake.Output.carCommands.Count && brake.Output.hasCommands)
                    {
                        var command = brake.Output.carCommands[i];
                        text.AppendLine($"  指令: 回生 {command.targetRegenForceN:F0} N、空制 {command.targetAirForceN:F0} N / {command.targetAirPressureKPa:F1} kPa");
                    }
                }
            foreach (var eb in ebDevices)
                if (eb != null) text.AppendLine($"EB {eb.name}: 残り {eb.Output.remainingSeconds:F1} s / 非常 {eb.IsEmergencyBrakeRequested}");
            return text.ToString();
        }
    }
}
