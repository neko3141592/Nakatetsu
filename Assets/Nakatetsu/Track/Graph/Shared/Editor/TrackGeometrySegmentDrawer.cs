using System;
using System.Reflection;
using Nakatetsu.Track.Graph.Geometry;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Editor
{
    public abstract class TrackGeometrySegmentDrawer : PropertyDrawer
    {
        protected abstract Type[] SegmentTypes { get; }
        protected abstract string[] TypeLabels { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldout = new Rect(header.x, header.y, EditorGUIUtility.labelWidth, header.height);
            property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, label, true);

            Rect selector = new Rect(foldout.xMax, header.y, header.width - foldout.width, header.height);
            Type currentType = property.managedReferenceValue?.GetType();
            int currentIndex = Array.IndexOf(SegmentTypes, currentType);
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUI.Popup(selector, currentIndex, TypeLabels);
            if (EditorGUI.EndChangeCheck())
            {
                SetSegmentType(property, SegmentTypes[selectedIndex]);
                property.serializedObject.ApplyModifiedProperties();
                EditorGUI.EndProperty();
                return;
            }

            if (property.isExpanded && currentType != null)
            {
                EditorGUI.indentLevel++;
                SerializedProperty child = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                float y = header.yMax + EditorGUIUtility.standardVerticalSpacing;
                if (child.NextVisible(true))
                {
                    while (!SerializedProperty.EqualContents(child, end))
                    {
                        float height = EditorGUI.GetPropertyHeight(child, true);
                        EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
                        y += height + EditorGUIUtility.standardVerticalSpacing;
                        if (!child.NextVisible(false)) break;
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded || property.managedReferenceValue == null) return height;

            SerializedProperty child = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            if (child.NextVisible(true))
            {
                while (!SerializedProperty.EqualContents(child, end))
                {
                    height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                    if (!child.NextVisible(false)) break;
                }
            }
            return height;
        }

        public static void SetSegmentType(SerializedProperty property, Type segmentType)
        {
            object previous = property.managedReferenceValue;
            object replacement = segmentType == null ? null : Activator.CreateInstance(segmentType);
            if (previous != null && replacement != null)
            {
                // 区間位置・長さ・半径など、変更先にもあるパラメータを引き継ぐ。
                foreach (FieldInfo field in previous.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    FieldInfo destination = segmentType.GetField(field.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (destination != null && destination.FieldType == field.FieldType)
                        destination.SetValue(replacement, field.GetValue(previous));
                }

                if (previous is TrackGeometryConstantGradientSegment constant &&
                    replacement is TrackGeometryLinearGradientSegment linear)
                {
                    linear.startGradientPermille = constant.gradientPermille;
                    linear.endGradientPermille = constant.gradientPermille;
                }
                else if (previous is TrackGeometryLinearGradientSegment previousLinear &&
                         replacement is TrackGeometryConstantGradientSegment newConstant)
                {
                    newConstant.gradientPermille = previousLinear.startGradientPermille;
                }
            }
            property.managedReferenceValue = replacement;
            property.isExpanded = replacement != null;
        }
    }

    [CustomPropertyDrawer(typeof(TrackGeometryHorizontalSegment), true)]
    public sealed class TrackGeometryHorizontalSegmentDrawer : TrackGeometrySegmentDrawer
    {
        private static readonly Type[] Types =
        {
            null, typeof(TrackGeometryStraightSegment), typeof(TrackGeometryCircularSegment),
            typeof(TrackGeometryTransitionInSegment), typeof(TrackGeometryTransitionOutSegment)
        };
        private static readonly string[] Labels = { "None", "Straight", "Circular", "Transition In", "Transition Out" };
        protected override Type[] SegmentTypes => Types;
        protected override string[] TypeLabels => Labels;
    }

    [CustomPropertyDrawer(typeof(TrackGeometryVerticalSegment), true)]
    public sealed class TrackGeometryVerticalSegmentDrawer : TrackGeometrySegmentDrawer
    {
        private static readonly Type[] Types =
        {
            null, typeof(TrackGeometryConstantGradientSegment), typeof(TrackGeometryLinearGradientSegment)
        };
        private static readonly string[] Labels = { "None", "Constant Gradient", "Linear Gradient" };
        protected override Type[] SegmentTypes => Types;
        protected override string[] TypeLabels => Labels;
    }
}
