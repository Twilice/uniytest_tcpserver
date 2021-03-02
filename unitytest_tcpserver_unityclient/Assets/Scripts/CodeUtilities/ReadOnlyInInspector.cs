using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/*
    --- Usage ---
public class ReadOnlyTest : MonoBehaviour
{
  [ReadOnlyInInspector]
  public bool notInspectorEditable = true;
}

    --- todo ---
    need to find a way to disable list/array size/length

 */
namespace UnityEngine
{
    public class ReadOnlyInInspectorAttribute : PropertyAttribute
    {

    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyInInspectorAttribute))]
    public class ReadOnlyInInspectorDrawer : PropertyDrawer
    {
        // need to include children height or the inspector will overdraw for readonly proprties with children.
        public override float GetPropertyHeight(SerializedProperty property,
                                                GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
}