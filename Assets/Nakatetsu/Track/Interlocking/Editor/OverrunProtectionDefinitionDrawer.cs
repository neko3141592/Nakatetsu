using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nakatetsu.Track.Interlocking.Editor
{
    [CustomPropertyDrawer(typeof(OverrunProtectionDefinition))]
    public sealed class OverrunProtectionDefinitionDrawer : PropertyDrawer
    {
        private static readonly string[] ChildNames = { "common", "normalAdditional", "release" };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var toggle = new Toggle(property.displayName)
            {
                tooltip = "有効にすると過走防護を設定できます。無効にすると設定を削除し、過走防護なしになります。"
            };
            toggle.AddToClassList(BaseField<bool>.alignedFieldUssClassName);
            root.Add(toggle);
            var fields = new VisualElement();
            fields.style.marginLeft = 15;
            root.Add(fields);
            long displayedReferenceId = long.MinValue;

            void Refresh()
            {
                toggle.SetValueWithoutNotify(property.managedReferenceValue != null);
                toggle.showMixedValue = property.hasMultipleDifferentValues;
                if (displayedReferenceId == property.managedReferenceId)
                    return;

                displayedReferenceId = property.managedReferenceId;
                fields.Clear();
                if (property.managedReferenceValue == null)
                    return;

                foreach (string childName in ChildNames)
                {
                    var field = new PropertyField(property.FindPropertyRelative(childName));
                    fields.Add(field);
                    field.Bind(property.serializedObject);
                }
            }

            toggle.RegisterValueChangedCallback(evt =>
            {
                property.managedReferenceValue = evt.newValue ? new OverrunProtectionDefinition() : null;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            root.TrackPropertyValue(property, _ => Refresh());
            Refresh();
            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue != null)
            {
                foreach (string childName in ChildNames)
                    height += EditorGUIUtility.standardVerticalSpacing +
                              EditorGUI.GetPropertyHeight(property.FindPropertyRelative(childName), true);
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            bool enabled = property.managedReferenceValue != null;
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            bool nextEnabled = EditorGUI.Toggle(line, label, enabled);
            if (EditorGUI.EndChangeCheck())
                property.managedReferenceValue = nextEnabled ? new OverrunProtectionDefinition() : null;
            EditorGUI.showMixedValue = previousMixedValue;

            if (property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                foreach (string childName in ChildNames)
                {
                    var child = property.FindPropertyRelative(childName);
                    line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                    line.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(line, child, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
