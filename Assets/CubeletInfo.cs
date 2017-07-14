using UnityEngine;

namespace RubiksCube
{
    sealed class CubeletInfo
    {
        public Transform Cubelet { get; private set; }
        public Quaternion Rotation { get; private set; }
        public CubeletInfo(Transform cubelet, Quaternion rotation)
        {
            Cubelet = cubelet;
            Rotation = rotation;
        }
    }
}
