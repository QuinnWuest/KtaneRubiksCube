using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Rubik's Cube
/// Created by Timwi
/// </summary>
public class RubiksCubeModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    void Start()
    {
        Debug.Log("[Rubik's Cube] Started");
    }

    void ActivateModule()
    {
        Debug.Log("[Rubik's Cube] Activated");
    }
}
