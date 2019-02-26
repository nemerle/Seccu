using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AlwaysFaceCamera : MonoBehaviour
{
    public Camera m_Camera;

    private void Awake()
    {
        if (!m_Camera)
            m_Camera = Camera.main;
    }


    // Update is called once per frame
    void LateUpdate()
    {
        if (m_Camera == null)
            return;
        transform.LookAt(m_Camera.transform.position, m_Camera.transform.rotation * Vector3.up);
    }
}
