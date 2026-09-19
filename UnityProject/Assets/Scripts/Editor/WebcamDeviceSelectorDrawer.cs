using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(WebcamDeviceSelectorAttribute))]
public class WebcamDeviceSelectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Ensure the attribute is only used on string fields
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Error: Use [WebcamDeviceSelector] with a string.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        // Fetch available cameras
        WebCamDevice[] devices = WebCamTexture.devices;
        
        if (devices.Length == 0)
        {
            EditorGUI.LabelField(position, label.text, "No cameras found");
        }
        else
        {
            string[] deviceNames = new string[devices.Length];
            int selectedIndex = -1;

            // Populate dropdown options and find the currently saved index
            for (int i = 0; i < devices.Length; i++)
            {
                string prefix = devices[i].isFrontFacing ? "[Front] " : "[Back] ";
                deviceNames[i] = prefix + devices[i].name;

                if (devices[i].name == property.stringValue)
                {
                    selectedIndex = i;
                }
            }

            // Fallback if the saved device name is no longer available
            if (selectedIndex == -1)
            {
                selectedIndex = 0;
            }

            // FIX: Use label.text (string) instead of label (GUIContent) to match the string[] overload
            int newSelectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, deviceNames);

            // Save the new selection to the serialized property
            if (newSelectedIndex != selectedIndex && newSelectedIndex >= 0 && newSelectedIndex < devices.Length)
            {
                property.stringValue = devices[newSelectedIndex].name;
            }
        }

        EditorGUI.EndProperty();
    }
}