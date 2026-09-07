using UnityEngine;
using Nakatetsu.Train.Tims.Communication;
using Nakatetsu.Train.Tims.Bus;
using UnityEngine.UI;
using TMPro;

namespace Nakatetsu.Train.Tims.Presentation.Indicators
{
    public enum TimsBusTarget
    {
        Master,
        Local
    }

    public sealed class TimsBoolIndicator : MonoBehaviour {

        [Header("Tims")]
        [SerializeField]
        private TimsCommunicationController tims;

        [Header("Binding")]
        [SerializeField]
        private string deviceName;

        [SerializeField]
        private string itemName;

        [Header("Bus")]
        [SerializeField]
        private TimsBusTarget target;

        [SerializeField, Min(0)]
        private int localCarIndex;

        [Header("Objects")]
        [SerializeField]
        private Image background;

        [SerializeField]
        private TMP_Text labelText;

        [Header("Colors")]
        [SerializeField]
        private Color backgroundOnColor = Color.green;

        [SerializeField]
        private Color backgroundOffColor = Color.black;

        [SerializeField]
        private Color labelOnColor = Color.black;

        [SerializeField]
        private Color labelOffColor = Color.gray;

        private void Awake()
        {
            background = GetComponentInChildren<Image>(true);
            labelText = GetComponentInChildren<TMP_Text>(true);
        }


        private TimsBusState GetBus()
        {
            if (tims == null)
            {
                return null;
            }

            return target switch
            {
                TimsBusTarget.Master =>
                    tims.MasterBus,

                TimsBusTarget.Local =>
                    tims.GetLocalBus(localCarIndex),

                _ => null
            };
        }

        
        private void LateUpdate() {

            TimsBusState timsBusState = GetBus();

            if (timsBusState == null)
            {
                return;
            }

            if (timsBusState.TryGetBool(new TimsTagKey(deviceName, itemName), out bool isOn))
            {
                background.color = isOn ? backgroundOnColor : backgroundOffColor;
                labelText.color = isOn ? labelOnColor : labelOffColor;
            } else
            {
                background.color = backgroundOffColor;
                labelText.color = labelOffColor;
            }


        }
    
    }
}
