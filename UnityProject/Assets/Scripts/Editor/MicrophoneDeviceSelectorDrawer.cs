using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(MicrophoneDeviceSelectorAttribute))]
public class MicrophoneDeviceSelectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Error: Use [MicrophoneDeviceSelector] with a string.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        // Fetch available microphones
        string[] devices = Microphone.devices;
        
        if (devices.Length == 0)
        {
            EditorGUI.LabelField(position, label.text, "No microphones found");
        }
        else
        {
            int selectedIndex = -1;

            // Find the currently saved device name
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i] == property.stringValue)
                {
                    selectedIndex = i;
                }
            }

            // Fallback if the saved device name is no longer available
            if (selectedIndex == -1)
            {
                selectedIndex = 0;
            }

            // Draw the dropdown
            int newSelectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, devices);

            // Save the new selection
            if (newSelectedIndex != selectedIndex && newSelectedIndex >= 0 && newSelectedIndex < devices.Length)
            {
                property.stringValue = devices[newSelectedIndex];
            }
        }

        EditorGUI.EndProperty();
    }
}