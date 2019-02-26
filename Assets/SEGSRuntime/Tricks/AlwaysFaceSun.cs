using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AlwaysFaceSun : MonoBehaviour
{
    private GameObject m_Sun;

    private void Awake()
    {
        if (m_Sun)
            return; // we're ready.

        m_Sun = GameObject.Find("Sun");
        if (!m_Sun)
            Debug.LogWarning("Cannot find 'Sun' object to align to.");
    }


    // Update is called once per frame
    void LateUpdate()
    {
        if (m_Sun == null)
            return;

        var tgtpos = m_Sun.transform.position;
        // we basically 'project' the sun's position to the 'ground' level of this object,
        // then rotate to face this projection
        Vector3 targetPosition = new Vector3(tgtpos.x,
            this.transform.position.y,
            tgtpos.z);
        transform.LookAt(targetPosition);
    }
}