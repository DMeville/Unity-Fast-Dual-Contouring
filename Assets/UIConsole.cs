using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIConsole : MonoBehaviour {


    public static UIConsole instance;
    public UnityEngine.UI.Text text;
    // Use this for initialization

    public void Awake() {
        instance = this;
        text = this.gameObject.GetComponent<UnityEngine.UI.Text>();
    }
    
    public void AddText(string t) {
        text.text += t;
    }
}
