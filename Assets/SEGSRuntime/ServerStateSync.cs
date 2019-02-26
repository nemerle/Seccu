using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerStateSync : MonoBehaviour
{
    // Time of day in a float 0.0->23.9999 form
    public float TimeOfDay { get; private set; }
    // Start is called before the first frame update
    void Start()
    {
        UpdateTime();
        InvokeRepeating("UpdateTime", 1.0f, 1.0f);
    }

    private void UpdateTime()
    {
        var start = DateTime.Now;
        TimeOfDay = (start.Hour * 60 * 60 + start.Minute * 60 + start.Second) / (60 * 60);
    }

}
