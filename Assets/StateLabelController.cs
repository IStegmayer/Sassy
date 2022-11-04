using System.Collections;
using System.Collections.Generic;
using Game._Scripts.Sassy;
using TMPro;
using UnityEngine;

public class StateLabelController : MonoBehaviour {
    public TMPro.TMP_Text _label;
    public void Start() => _label = GetComponent<TMP_Text>();
    public void SetLabelText(string text) {
        _label.text = text;
    }
}
