using System;
using RubiksCube;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Rubik's Cube
/// Created by Timwi and Freelancer1025
/// </summary>
public class RubiksCubeModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    private Transform[,,] _cubelets;
    public Material[] StickerMaterials;

    public Transform Cube;
    public Transform OffAxis;
    public Transform OnAxis;
    public GameObject Sticker;

    private Queue<FaceRotation> _queue = new Queue<FaceRotation>();
    private bool _isSolved = false;
    private int _moduleId;
    private static int _moduleIdCounter = 1;

    void Start()
    {
        Cube.localEulerAngles = new Vector3(25, 70, 60);
        _moduleId = _moduleIdCounter++;
        _cubelets = new Transform[3, 3, 3];
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                {
                    _cubelets[x, y, z] = Cube.Find("Layer" + (y + 1)).Find("Row" + (x + 1)).Find("Cubelet" + (3 - z));
                    if (y == 0)
                        CreateSticker(_cubelets[x, y, z], StickerMaterials[5], new Vector3(0, 0, 0), "Top sticker (Y)");
                    else if (y == 2)
                        CreateSticker(_cubelets[x, y, z], StickerMaterials[4], new Vector3(180, 0, 0), "Bottom sticker (W)");

                    if (x == 0)
                        CreateSticker(_cubelets[x, y, z], StickerMaterials[0], new Vector3(-90, 0, 0), "Left sticker (B)");
                    else if (x == 2)
                        CreateSticker(_cubelets[x, y, z], StickerMaterials[1], new Vector3(90, 0, 0), "Right sticker (G)");

                    if (z == 0)
                        CreateSticker(_cubelets[x, y, z], StickerMaterials[3], new Vector3(0, 0, -90), "Front sticker (R)");
                    else if (z == 2)
                        CreateSticker(_cubelets[x, y, z], StickerMaterials[2], new Vector3(0, 0, 90), "Back sticker (O)");
                }

        Module.OnActivate += ActivateModule;
    }

    private void CreateSticker(Transform cubelet, Material mat, Vector3 rotation, string name)
    {
        var stickerGo = cubelet.Find("Sticker");
        GameObject sticker = stickerGo != null ? stickerGo.gameObject : null;
        if (sticker == null)
        {
            sticker = Instantiate(Sticker);
            sticker.transform.parent = cubelet;
            sticker.transform.localPosition = new Vector3(0, 0, 0);
        }
        sticker.GetComponent<MeshRenderer>().material = mat;
        sticker.transform.localScale = new Vector3(1, 1, 1);
        sticker.transform.localEulerAngles = rotation;
        sticker.name = name;
    }

    void ActivateModule()
    {
        var table = @"L',F';D',U';U,B';F,B;L,D;R',U;U',F;B',L';B,R;D,L;R,D';F',R'".Split(';').Select(row => row.Split(',')).ToArray();
        var colShifts = new int[3];
        foreach (var port in Bomb.GetPorts())
            switch (port)
            {
                case "PS2": colShifts[0]++; break;
                case "Parallel": colShifts[1]++; break;
                case "DVI": colShifts[2]++; break;
                case "Serial": colShifts[0] += 2; break;
                case "StereoRCA": colShifts[1] += 2; break;
                case "RJ45": colShifts[2] += 2; break;
            }
        Debug.LogFormat("[Rubik's Cube #{0}] Column shifts: A={1}, B={2}, C={3}", _moduleId, colShifts[0], colShifts[1], colShifts[2]);
        var rows = Bomb.GetSerialNumber().Select(ch => ch >= '0' && ch <= '9' ? ch - '0' : ch - 'A' + 10).Select(n => ((n / 3 - colShifts[n % 3]) % table.Length + table.Length) % table.Length).ToArray();
        string[] moves;
        if (Bomb.GetPortPlates().Any(p => p.Length == 0))
            moves = rows.Select(r => table[r][0]).Concat(rows.Select(r => table[r][1])).ToArray();
        else
            moves = rows.SelectMany(r => table[r]).ToArray();
        if (Bomb.GetOnIndicators().Count() >= Bomb.GetOffIndicators().Count())
            for (int i = 0; i < 6; i++)
                moves[i] = opposite(moves[i]);

        Debug.LogFormat("[Rubik's Cube #{0}] Solution moves: {1}", _moduleId, string.Join(", ", moves));

        foreach (var move in moves.Reverse())
            _queue.Enqueue(_moves[opposite(move)]);
        StartCoroutine(PerformMoves());
    }

    private string opposite(string move)
    {
        if (move.EndsWith("'"))
            return move.Substring(0, 1);
        return move + "'";
    }

    private IEnumerator PerformMoves()
    {
        while (!_isSolved)
        {
            yield return null;
            if (_queue.Count > 0)
            {
                var move = _queue.Dequeue();
                foreach (var obj in PerformRotation(move))
                    yield return obj;
            }
        }
    }

    sealed class FaceRotation
    {
        public Func<int, int, int, bool> OnAxis { get; private set; }
        public Func<int, Vector3> Rotation { get; private set; }
        public Func<int, int, int, int> MapX { get; private set; }
        public Func<int, int, int, int> MapY { get; private set; }
        public Func<int, int, int, int> MapZ { get; private set; }
        public FaceRotation(Func<int, int, int, bool> onAxis, Func<int, Vector3> rotation, Func<int, int, int, int> mapX, Func<int, int, int, int> mapY, Func<int, int, int, int> mapZ)
        {
            OnAxis = onAxis;
            Rotation = rotation;
            MapX = mapX;
            MapY = mapY;
            MapZ = mapZ;
        }
    }

    private static Dictionary<string, FaceRotation> _moves;

    static RubiksCubeModule()
    {
        _moves = new Dictionary<string, FaceRotation>(StringComparer.InvariantCultureIgnoreCase);
        _moves["f"] = new FaceRotation((x, y, z) => z == 0, i => new Vector3(i, 0, 0), (x, y, z) => y, (x, y, z) => 2 - x, (x, y, z) => z);
        _moves["f'"] = new FaceRotation((x, y, z) => z == 0, i => new Vector3(-i, 0, 0), (x, y, z) => 2 - y, (x, y, z) => x, (x, y, z) => z);
        _moves["b"] = new FaceRotation((x, y, z) => z == 2, i => new Vector3(-i, 0, 0), (x, y, z) => 2 - y, (x, y, z) => x, (x, y, z) => z);
        _moves["b'"] = new FaceRotation((x, y, z) => z == 2, i => new Vector3(i, 0, 0), (x, y, z) => y, (x, y, z) => 2 - x, (x, y, z) => z);
        _moves["l"] = new FaceRotation((x, y, z) => x == 0, i => new Vector3(0, 0, -i), (x, y, z) => x, (x, y, z) => z, (x, y, z) => 2 - y);
        _moves["l'"] = new FaceRotation((x, y, z) => x == 0, i => new Vector3(0, 0, i), (x, y, z) => x, (x, y, z) => 2 - z, (x, y, z) => y);
        _moves["r"] = new FaceRotation((x, y, z) => x == 2, i => new Vector3(0, 0, i), (x, y, z) => x, (x, y, z) => 2 - z, (x, y, z) => y);
        _moves["r'"] = new FaceRotation((x, y, z) => x == 2, i => new Vector3(0, 0, -i), (x, y, z) => x, (x, y, z) => z, (x, y, z) => 2 - y);
        _moves["u"] = new FaceRotation((x, y, z) => y == 0, i => new Vector3(0, i, 0), (x, y, z) => 2 - z, (x, y, z) => y, (x, y, z) => x);
        _moves["u'"] = new FaceRotation((x, y, z) => y == 0, i => new Vector3(0, -i, 0), (x, y, z) => z, (x, y, z) => y, (x, y, z) => 2 - x);
        _moves["d"] = new FaceRotation((x, y, z) => y == 2, i => new Vector3(0, -i, 0), (x, y, z) => z, (x, y, z) => y, (x, y, z) => 2 - x);
        _moves["d'"] = new FaceRotation((x, y, z) => y == 2, i => new Vector3(0, i, 0), (x, y, z) => 2 - z, (x, y, z) => y, (x, y, z) => x);
    }

    IEnumerator ProcessTwitchCommand(string command)
    {
        foreach (var cmd in command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            FaceRotation rot;
            if (_moves.TryGetValue(cmd, out rot))
                foreach (var obj in PerformRotation(rot))
                    yield return obj;
            else if (cmd.Length == 2 && cmd[1] == '2' && _moves.TryGetValue(cmd[0].ToString(), out rot))
            {
                foreach (var obj in PerformRotation(rot))
                    yield return obj;
                foreach (var obj in PerformRotation(rot))
                    yield return obj;
            }
        }
    }

    IEnumerable PerformRotation(FaceRotation rot, bool instantaneous = false)
    {
        var newCubelets = new Transform[3, 3, 3];
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                    if (rot.OnAxis(x, y, z))
                    {
                        _cubelets[x, y, z].parent = OnAxis;
                        newCubelets[x, y, z] = _cubelets[rot.MapX(x, y, z), rot.MapY(x, y, z), rot.MapZ(x, y, z)];
                    }
                    else
                        newCubelets[x, y, z] = _cubelets[x, y, z];
        _cubelets = newCubelets;

        for (int i = instantaneous ? 90 : 0; i <= 90; i += 5)
        {
            OnAxis.localEulerAngles = rot.Rotation(i);
            yield return null;
        }

        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                    _cubelets[x, y, z].parent = OffAxis;

        OnAxis.localEulerAngles = new Vector3(0, 0, 0);
    }
}
