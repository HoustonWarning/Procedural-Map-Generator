using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateData : ScriptableObject {

    public event System.Action OnValueUpdate;
    public bool autoUpdate;

#if UNITY_EDITOR

    protected virtual void OnValidate()
    {
        if (autoUpdate)
        {
            UnityEditor.EditorApplication.update += NotifyOfValueUpdates;
        }
    }

    public void NotifyOfValueUpdates()
    {
        UnityEditor.EditorApplication.update -= NotifyOfValueUpdates;
        if (OnValueUpdate != null)
        {
            OnValueUpdate();
        }
    }

#endif
}
