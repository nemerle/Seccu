using System;
using System.Collections.Generic;
using UnityEngine;

namespace SEGSRuntime
{
    [Serializable]
    public struct CoHNodeData
    {
        public List<GroupProperty_Data2> Properties;
    }
    public class NodeMods : MonoBehaviour
    {
        [SerializeField] private bool IsMapEditorNode;
        [SerializeField] public CoHNodeData Props;

        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
