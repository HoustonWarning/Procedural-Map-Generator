using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UpdateData), true)]
public class NewBehaviourScript : Editor {

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        UpdateData data = (UpdateData)target;

        if (GUILayout.Button("Update"))
        {
            data.NotifyOfValueUpdates();
            EditorUtility.SetDirty(target);
        }
    }
}
