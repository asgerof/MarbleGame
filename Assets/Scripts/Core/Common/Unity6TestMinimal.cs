using UnityEngine;
using System;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Minimal test to verify Unity 6 basic functionality
    /// </summary>
    public class Unity6TestMinimal : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("Unity 6 Test: Basic functionality working");
            
            // Test basic math
            float testValue = 3.14f;
            Vector3 testVector = new Vector3(1, 2, 3);
            Debug.Log($"Basic math test: {testValue} and vector: {testVector}");
        }
    }
} 