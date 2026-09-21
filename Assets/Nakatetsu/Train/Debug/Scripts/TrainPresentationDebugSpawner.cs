using System;
using Nakatetsu.Train.Presentation.Shared;
using UnityEngine;

namespace Nakatetsu.Train.Debugging
{
    /// <summary>グラフ完成までの確認用。車両モデルを格子状に配置し、所属車両を割り当てる。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Train/Debug/Presentation Spawner")]
    public sealed class TrainPresentationDebugSpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class CarModel
        {
            public GameObject prefab;
            [Tooltip("デバッグ配置での車両ルートの回転。Tc2はY=180で他車とドアの左右・前後を揃えます。")]
            public Vector3 rotationEuler;
        }

        [SerializeField] private TrainRoot trainRoot;
        [SerializeField] private bool generateOnStart = true;
        [Tooltip("編成先頭から順に指定。要素0が1両目です。")]
        [SerializeField] private CarModel[] cars = new CarModel[10];

        [Header("Layout")]
        [Tooltip("TrainRootのローカル座標での配置開始位置。")]
        [SerializeField] private Vector3 originPositionM;
        [SerializeField, Min(1)] private int carsPerRow = 5;
        [Tooltip("Xは横方向、Yは列間の後方距離[m]。初期設定では5両ずつ2列です。")]
        [SerializeField] private Vector2 spacingM = new(6f, 25f);
        [SerializeField, HideInInspector] private Transform generatedRoot;

        public Transform GeneratedRoot => generatedRoot;

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("Generate presentation (Play Mode only)")]
        public void Generate()
        {
            if (!Application.isPlaying) return;
            if (trainRoot == null) trainRoot = GetComponentInParent<TrainRoot>(true);
            if (trainRoot == null || trainRoot.ConsistDefinition == null)
            {
                Debug.LogWarning($"{nameof(TrainPresentationDebugSpawner)}: 編成定義のあるTrainRootの子に配置してください。", this);
                return;
            }

            int carCount = trainRoot.ConsistDefinition.CarCount;
            if (carCount == 0 || cars == null || cars.Length != carCount)
            {
                Debug.LogError($"{nameof(TrainPresentationDebugSpawner)}: 編成両数({carCount})と同じ数のPrefabを指定してください。", this);
                return;
            }

            for (int i = 0; i < carCount; i++)
            {
                if (trainRoot.ConsistDefinition.cars[i] == null || cars[i] == null || cars[i].prefab == null)
                {
                    Debug.LogError($"{nameof(TrainPresentationDebugSpawner)}: {i + 1}両目のDefinitionまたはPrefabがありません。", this);
                    return;
                }
            }

            OnValidate();
            ClearGenerated();
            var container = new GameObject("DebugPresentation");
            // 戸のAwake/Startより前に全車のAssignmentを設定する。
            container.SetActive(false);
            generatedRoot = container.transform;
            generatedRoot.SetParent(trainRoot.transform, false);
            generatedRoot.localPosition = originPositionM;

            for (int carIndex = 0; carIndex < carCount; carIndex++)
            {
                CarModel model = cars[carIndex];
                GameObject car = Instantiate(model.prefab, generatedRoot, false);
                car.name = $"Car_{carIndex + 1:00}_{model.prefab.name}";
                car.transform.localPosition = new Vector3(
                    (carIndex % carsPerRow) * spacingM.x,
                    0f,
                    -(carIndex / carsPerRow) * spacingM.y);
                car.transform.localRotation = Quaternion.Euler(model.rotationEuler);

                var assignment = car.GetComponent<TrainPresentationAssignment>();
                if (assignment == null) assignment = car.AddComponent<TrainPresentationAssignment>();
                assignment.AssignCarIndex(carIndex);
            }

            container.SetActive(true);
        }

        [ContextMenu("Clear generated presentation")]
        public void ClearGenerated()
        {
            if (generatedRoot == null) return;
            GameObject container = generatedRoot.gameObject;
            generatedRoot = null;
            container.SetActive(false);
            if (Application.isPlaying) Destroy(container);
            else DestroyImmediate(container);
        }

        private void OnDestroy() => ClearGenerated();

        private void OnValidate()
        {
            carsPerRow = Mathf.Max(1, carsPerRow);
            spacingM.x = Mathf.Max(1f, spacingM.x);
            spacingM.y = Mathf.Max(1f, spacingM.y);
        }
    }
}
